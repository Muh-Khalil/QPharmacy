// ═══════════════════════════════════════════════════════════════════════════════
// WebRTC Interop Module for QPharmacy Telehealth Consultations
// ═══════════════════════════════════════════════════════════════════════════════

window.webrtcInterop = {
    localStream: null,
    remoteStream: null,
    peerConnection: null,
    hubConnection: null,
    roomId: null,
    isCaller: false,
    candidateQueue: [],
    _bindingTimer: null,
    _animatingCanvas: false,
    _isEnding: false,

    // STUN Servers for NAT Traversal
    configuration: {
        iceServers: [
            { urls: 'stun:stun.l.google.com:19302' },
            { urls: 'stun:stun1.l.google.com:19302' },
            { urls: 'stun:stun2.l.google.com:19302' }
        ]
    },

    // ─────────────────────────────────────────────────────────────────
    // 1. INITIALIZATION & LIFECYCLE
    // ─────────────────────────────────────────────────────────────────

    initialize: async function (hubUrl, room, caller, dotNetHelper) {
        this._isEnding = false;
        this.roomId = room;
        this.isCaller = caller;
        this.dotNetHelper = dotNetHelper || null;
        this.candidateQueue = [];

        console.log(`[WebRTC] Initializing: Room=${this.roomId}, IsCaller=${this.isCaller}`);

        // Clean up any stale state first
        if (this.peerConnection) {
            try { this.peerConnection.close(); } catch {}
            this.peerConnection = null;
        }

        // STEP 1: Acquire local media (hardware camera/mic or simulated fallback)
        await this.acquireLocalStream();

        // STEP 2: Start persistent element binder to attach local/remote video as DOM renders
        this.startVideoBindingLoop();

        // STEP 3: Create Peer Connection and attach tracks
        this.createPeerConnection();

        // STEP 4: Setup SignalR Connection
        if (this.hubConnection) {
            try { await this.hubConnection.stop(); } catch {}
            this.hubConnection = null;
        }

        this.hubConnection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl)
            .withAutomaticReconnect([0, 1000, 2000, 5000])
            .build();

        // Signal: Peer joined room
        this.hubConnection.on("UserJoined", async () => {
            console.log("[WebRTC] Peer joined room event received");
            if (this.isCaller) {
                console.log("[WebRTC] As caller, generating offer for peer...");
                await this.sendOffer();
            } else {
                console.log("[WebRTC] As callee, sending UserReady signal to caller...");
                try {
                    await this.hubConnection.invoke("UserReady", this.roomId);
                } catch (e) {
                    console.warn("[WebRTC] Error sending UserReady:", e);
                }
            }
        });

        // Signal: Callee is ready for offer
        this.hubConnection.on("UserReady", async () => {
            console.log("[WebRTC] Peer signaled UserReady");
            if (this.isCaller) {
                await this.sendOffer();
            }
        });

        // Signal: Receive Offer
        this.hubConnection.on("ReceiveOffer", async (offerStr) => {
            console.log("[WebRTC] Received WebRTC Offer from peer");
            try {
                if (!this.peerConnection || this.peerConnection.signalingState === "closed") {
                    this.createPeerConnection();
                }
                const offer = JSON.parse(offerStr);
                
                // If caller collision occurs, rollback or handle state
                if (this.peerConnection.signalingState !== "stable") {
                    console.log("[WebRTC] Signaling state not stable during offer:", this.peerConnection.signalingState);
                    await Promise.all([
                        this.peerConnection.setLocalDescription({ type: "rollback" }).catch(() => {}),
                    ]);
                }

                await this.peerConnection.setRemoteDescription(new RTCSessionDescription(offer));
                console.log("[WebRTC] Remote description (offer) set successfully");

                // Flush queued ICE candidates
                while (this.candidateQueue.length > 0) {
                    const cand = this.candidateQueue.shift();
                    try {
                        await this.peerConnection.addIceCandidate(new RTCIceCandidate(JSON.parse(cand)));
                    } catch (ce) {
                        console.warn("[WebRTC] Queued candidate failed:", ce);
                    }
                }

                const answer = await this.peerConnection.createAnswer();
                await this.peerConnection.setLocalDescription(answer);
                await this.hubConnection.invoke("SendAnswer", JSON.stringify(answer), this.roomId);
                console.log("[WebRTC] Answer dispatched to caller in room:", this.roomId);
            } catch (err) {
                console.error("[WebRTC] Error handling offer:", err);
            }
        });

        // Signal: Receive Answer
        this.hubConnection.on("ReceiveAnswer", async (answerStr) => {
            console.log("[WebRTC] Received WebRTC Answer from peer");
            try {
                if (this.peerConnection) {
                    const answer = JSON.parse(answerStr);
                    await this.peerConnection.setRemoteDescription(new RTCSessionDescription(answer));
                    console.log("[WebRTC] Remote description (answer) set successfully");

                    // Flush queued ICE candidates
                    while (this.candidateQueue.length > 0) {
                        const cand = this.candidateQueue.shift();
                        try {
                            await this.peerConnection.addIceCandidate(new RTCIceCandidate(JSON.parse(cand)));
                        } catch (ce) {
                            console.warn("[WebRTC] Queued candidate failed:", ce);
                        }
                    }
                }
            } catch (err) {
                console.error("[WebRTC] Error handling answer:", err);
            }
        });

        // Signal: Receive ICE Candidate
        this.hubConnection.on("ReceiveIceCandidate", async (candidateStr) => {
            if (this.peerConnection && this.peerConnection.remoteDescription && this.peerConnection.remoteDescription.type) {
                try {
                    await this.peerConnection.addIceCandidate(new RTCIceCandidate(JSON.parse(candidateStr)));
                } catch (e) {
                    console.warn("[WebRTC] Error adding ICE candidate:", e);
                }
            } else {
                this.candidateQueue.push(candidateStr);
            }
        });

        // Signal: Chat Messages
        this.hubConnection.on("ReceiveChatMessage", (senderName, messageText, isDoctor, timestamp, senderId) => {
            window.dispatchEvent(new CustomEvent("consultationChatMsg", {
                detail: { senderName, messageText, isDoctor, timestamp, senderId }
            }));
        });

        // Signal: Call Ended by other peer
        this.hubConnection.on("CallEnded", async () => {
            console.log("[WebRTC] CallEnded event received from hub");
            await this.endCall(false); // local cleanup without notifying hub again
            window.dispatchEvent(new CustomEvent("consultationCallEnded"));
            if (this.dotNetHelper) {
                try {
                    await this.dotNetHelper.invokeMethodAsync("OnRemoteCallEnded");
                } catch (e) {
                    console.warn("[WebRTC] OnRemoteCallEnded invocation failed:", e);
                }
            }
        });

        // Signal: Consultation ended in room (broadcast fallback)
        this.hubConnection.on("ConsultationEndedInRoom", async (endedRoom) => {
            if (endedRoom && this.roomId && endedRoom.toLowerCase() === this.roomId.toLowerCase()) {
                console.log("[WebRTC] ConsultationEndedInRoom event received for:", endedRoom);
                await this.endCall(false);
                window.dispatchEvent(new CustomEvent("consultationCallEnded"));
                if (this.dotNetHelper) {
                    try {
                        await this.dotNetHelper.invokeMethodAsync("OnRemoteCallEnded");
                    } catch (e) {
                        console.warn("[WebRTC] OnRemoteCallEnded invocation failed:", e);
                    }
                }
            }
        });

        // Signal: Remote peer toggled camera or mic
        this.hubConnection.on("ReceiveRemoteMediaStatus", (remoteCameraOff, remoteMicMuted) => {
            console.log(`[WebRTC] Remote media status changed: CameraOff=${remoteCameraOff}, MicMuted=${remoteMicMuted}`);
            if (this.dotNetHelper) {
                try {
                    this.dotNetHelper.invokeMethodAsync("OnRemoteMediaStatusChanged", remoteCameraOff, remoteMicMuted);
                } catch (e) {
                    console.warn("[WebRTC] OnRemoteMediaStatusChanged invocation failed:", e);
                }
            }
        });

        // Connect & Join Room
        try {
            await this.hubConnection.start();
            console.log("[WebRTC] SignalR Connected to ConsultationHub");
            await this.hubConnection.invoke("JoinRoom", this.roomId);

            // If caller, initiate offer after brief setup
            if (this.isCaller) {
                setTimeout(async () => {
                    await this.sendOffer();
                }, 800);
            } else {
                // If callee, announce readiness in case caller was already waiting
                setTimeout(async () => {
                    if (this.hubConnection && this.hubConnection.state === signalR.HubConnectionState.Connected) {
                        try { await this.hubConnection.invoke("UserReady", this.roomId); } catch {}
                    }
                }, 600);
            }
        } catch (e) {
            console.error("[WebRTC] SignalR connection failed:", e);
        }
    },

    // ─────────────────────────────────────────────────────────────────
    // 2. MEDIA ACQUISITION (HARDWARE WITH FALLBACK TO SIMULATED STREAM)
    // ─────────────────────────────────────────────────────────────────

    acquireLocalStream: async function () {
        // Stop any previous tracks
        if (this.localStream) {
            this.localStream.getTracks().forEach(t => t.stop());
            this.localStream = null;
        }

        try {
            this.localStream = await navigator.mediaDevices.getUserMedia({
                video: { width: { ideal: 1280 }, height: { ideal: 720 }, facingMode: "user" },
                audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true }
            });
            console.log("[WebRTC] Acquired hardware camera + microphone successfully.");
        } catch (camErr) {
            console.warn("[WebRTC] Hardware camera locked or permission denied, attempting audio-only:", camErr);
            try {
                this.localStream = await navigator.mediaDevices.getUserMedia({
                    audio: { echoCancellation: true, noiseSuppression: true }
                });
                console.log("[WebRTC] Acquired hardware microphone stream.");
            } catch (micErr) {
                console.warn("[WebRTC] Hardware microphone unavailable:", micErr);
                this.localStream = new MediaStream();
            }

            // Create animated canvas video track so WebRTC ALWAYS has a valid video stream
            try {
                const canvas = document.createElement("canvas");
                canvas.width = 640;
                canvas.height = 480;
                const ctx = canvas.getContext("2d");
                let pulseAngle = 0;

                const drawFrame = () => {
                    if (!this._animatingCanvas) return;
                    pulseAngle += 0.04;
                    ctx.fillStyle = "#090d16";
                    ctx.fillRect(0, 0, canvas.width, canvas.height);

                    // Ambient animated medical radar circle
                    const radius = 50 + Math.sin(pulseAngle) * 10;
                    ctx.beginPath();
                    ctx.arc(320, 200, radius, 0, Math.PI * 2);
                    ctx.fillStyle = this.isCaller ? "rgba(20, 184, 166, 0.2)" : "rgba(59, 130, 246, 0.2)";
                    ctx.fill();
                    ctx.lineWidth = 3;
                    ctx.strokeStyle = this.isCaller ? "#14b8a6" : "#3b82f6";
                    ctx.stroke();

                    // Central glowing dot
                    ctx.beginPath();
                    ctx.arc(320, 200, 12, 0, Math.PI * 2);
                    ctx.fillStyle = this.isCaller ? "#14b8a6" : "#3b82f6";
                    ctx.fill();

                    // Role label
                    ctx.fillStyle = "#ffffff";
                    ctx.font = "bold 22px system-ui, -apple-system, sans-serif";
                    ctx.textAlign = "center";
                    ctx.fillText(this.isCaller ? "Dr. Telehealth Room" : "Patient Telehealth Room", 320, 280);

                    // Status and timestamp
                    ctx.fillStyle = "#10b981";
                    ctx.font = "600 13px system-ui";
                    ctx.fillText("● Live Stream Active", 320, 310);

                    ctx.fillStyle = "#94a3b8";
                    ctx.font = "12px monospace";
                    ctx.fillText(new Date().toLocaleTimeString(), 320, 335);

                    requestAnimationFrame(drawFrame);
                };

                this._animatingCanvas = true;
                drawFrame();

                const canvasStream = canvas.captureStream(25);
                const videoTrack = canvasStream.getVideoTracks()[0];
                if (videoTrack) {
                    this.localStream.addTrack(videoTrack);
                }
            } catch (canvasErr) {
                console.error("[WebRTC] Canvas stream generation error:", canvasErr);
            }
        }

        // Ensure there is always at least one audio track (silent synth if hardware mic absent)
        if (this.localStream.getAudioTracks().length === 0) {
            try {
                const AudioContext = window.AudioContext || window.webkitAudioContext;
                if (AudioContext) {
                    const audioCtx = new AudioContext();
                    const dest = audioCtx.createMediaStreamDestination();
                    const osc = audioCtx.createOscillator();
                    const gain = audioCtx.createGain();
                    gain.gain.value = 0; // completely silent
                    osc.connect(gain);
                    gain.connect(dest);
                    osc.start();
                    const silentTrack = dest.stream.getAudioTracks()[0];
                    if (silentTrack) {
                        this.localStream.addTrack(silentTrack);
                    }
                }
            } catch (e) {
                console.warn("[WebRTC] Silent audio track creation failed:", e);
            }
        }
    },

    // ─────────────────────────────────────────────────────────────────
    // 3. PEER CONNECTION & TRACKS
    // ─────────────────────────────────────────────────────────────────

    createPeerConnection: function () {
        if (this.peerConnection) return;
        this.peerConnection = new RTCPeerConnection(this.configuration);

        // Attach local tracks immediately
        if (this.localStream) {
            this.localStream.getTracks().forEach(track => {
                this.peerConnection.addTrack(track, this.localStream);
                console.log("[WebRTC] Added track to PeerConnection:", track.kind);
            });
        }

        // ICE Candidate event
        this.peerConnection.onicecandidate = (event) => {
            if (event.candidate && this.hubConnection && this.hubConnection.state === signalR.HubConnectionState.Connected) {
                this.hubConnection.invoke("SendIceCandidate", JSON.stringify(event.candidate), this.roomId)
                    .catch(e => console.warn("[WebRTC] Error sending candidate:", e));
            }
        };

        // Remote track arrived
        this.peerConnection.ontrack = (event) => {
            console.log("[WebRTC] Remote track arrived:", event.track.kind);
            if (!this.remoteStream) {
                this.remoteStream = new MediaStream();
            }
            if (event.streams && event.streams[0]) {
                this.remoteStream = event.streams[0];
            } else {
                if (!this.remoteStream.getTracks().some(t => t.id === event.track.id)) {
                    this.remoteStream.addTrack(event.track);
                }
            }

            this.bindRemoteVideo();
            window.dispatchEvent(new CustomEvent('webrtcRemoteConnected'));
        };

        // Connection State Change
        this.peerConnection.onconnectionstatechange = () => {
            const state = this.peerConnection.connectionState;
            console.log("[WebRTC] Connection state changed:", state);
            if (state === "connected") {
                this.bindRemoteVideo();
                window.dispatchEvent(new CustomEvent('webrtcRemoteConnected'));
            } else if (state === "disconnected" || state === "failed") {
                console.warn("[WebRTC] Connection disconnected/failed.");
            }
        };
    },

    sendOffer: async function () {
        if (!this.peerConnection || !this.hubConnection) return;
        if (this.peerConnection.signalingState === "closed") return;

        try {
            // Re-verify local tracks attached
            if (this.localStream) {
                const senders = this.peerConnection.getSenders();
                this.localStream.getTracks().forEach(track => {
                    if (!senders.some(s => s.track && s.track.id === track.id)) {
                        this.peerConnection.addTrack(track, this.localStream);
                    }
                });
            }

            const offer = await this.peerConnection.createOffer({
                offerToReceiveAudio: true,
                offerToReceiveVideo: true
            });
            await this.peerConnection.setLocalDescription(offer);

            if (this.hubConnection.state === signalR.HubConnectionState.Connected) {
                await this.hubConnection.invoke("SendOffer", JSON.stringify(offer), this.roomId);
                console.log("[WebRTC] Offer dispatched to room:", this.roomId);
            }
        } catch (e) {
            console.error("[WebRTC] Error creating/sending offer:", e);
        }
    },

    // ─────────────────────────────────────────────────────────────────
    // 4. VIDEO ELEMENT BINDING (ROBUST DOM RETRY)
    // ─────────────────────────────────────────────────────────────────

    startVideoBindingLoop: function () {
        if (this._bindingTimer) clearInterval(this._bindingTimer);
        let iterations = 0;
        this._bindingTimer = setInterval(() => {
            iterations++;
            this.bindLocalVideo();
            this.bindRemoteVideo();
            if (iterations > 35) { // Stop polling after ~5 seconds
                clearInterval(this._bindingTimer);
                this._bindingTimer = null;
            }
        }, 150);
    },

    bindLocalVideo: function () {
        const localVideo = document.getElementById('localVideo');
        if (localVideo && this.localStream) {
            if (localVideo.srcObject !== this.localStream) {
                localVideo.srcObject = this.localStream;
            }
            localVideo.muted = true; // Always mute local video to avoid acoustic feedback!
            localVideo.play().catch(() => {});
            return true;
        }
        return false;
    },

    bindRemoteVideo: function () {
        const remoteVideo = document.getElementById('remoteVideo');
        if (remoteVideo && this.remoteStream) {
            if (remoteVideo.srcObject !== this.remoteStream) {
                remoteVideo.srcObject = this.remoteStream;
            }
            remoteVideo.play().catch(err => {
                console.warn("[WebRTC] Remote video autoplay blocked, muting to play:", err);
                remoteVideo.muted = true;
                remoteVideo.play().catch(() => {});
            });

            const placeholder = document.getElementById('remotePlaceholder');
            if (placeholder) {
                placeholder.classList.add('hidden');
                placeholder.style.display = 'none';
            }
            return true;
        }
        return false;
    },

    rebindVideoElements: function () {
        this.bindLocalVideo();
        this.bindRemoteVideo();
    },

    // ─────────────────────────────────────────────────────────────────
    // 5. CONTROLS & CLEANUP
    // ─────────────────────────────────────────────────────────────────

    toggleMic: function (isMuted) {
        if (this.localStream) {
            this.localStream.getAudioTracks().forEach(track => {
                track.enabled = !isMuted;
            });
            console.log(`[WebRTC] Microphone ${isMuted ? 'muted' : 'unmuted'}`);
        }
    },

    toggleCamera: function (isOff) {
        if (this.localStream) {
            this.localStream.getVideoTracks().forEach(track => {
                track.enabled = !isOff;
            });
            console.log(`[WebRTC] Camera ${isOff ? 'turned off' : 'turned on'}`);
        }
    },

    notifyCallEnded: async function (roomId) {
        const targetRoom = roomId || this.roomId;
        if (this.hubConnection && this.hubConnection.state === signalR.HubConnectionState.Connected && targetRoom) {
            try {
                console.log("[WebRTC] Notifying Hub of CallEnded in room:", targetRoom);
                await this.hubConnection.invoke("NotifyCallEnded", targetRoom);
            } catch (e) {
                console.warn("[WebRTC] NotifyCallEnded failed:", e);
            }
        }
    },

    sendChatMessage: async function (roomId, senderName, messageText, isDoctor, senderId) {
        const targetRoom = roomId || this.roomId;
        if (this.hubConnection && this.hubConnection.state === signalR.HubConnectionState.Connected && targetRoom) {
            try {
                await this.hubConnection.invoke("SendChatMessage", targetRoom, senderName, messageText, isDoctor, senderId || "");
            } catch (e) {
                console.warn("[WebRTC] sendChatMessage failed:", e);
            }
        }
    },

    endCall: async function (notify = true) {
        if (this._isEnding) return;
        this._isEnding = true;
        console.log("[WebRTC] Ending call and releasing media tracks...");

        if (this._bindingTimer) {
            clearInterval(this._bindingTimer);
            this._bindingTimer = null;
        }
        this._animatingCanvas = false;

        if (notify && this.roomId) {
            await this.notifyCallEnded(this.roomId);
        }

        // Stop all local camera/microphone tracks so device LEDs turn OFF
        if (this.localStream) {
            this.localStream.getTracks().forEach(track => {
                try { track.stop(); } catch {}
            });
            this.localStream = null;
        }

        if (this.remoteStream) {
            this.remoteStream.getTracks().forEach(track => {
                try { track.stop(); } catch {}
            });
            this.remoteStream = null;
        }

        // Close peer connection
        if (this.peerConnection) {
            try { this.peerConnection.close(); } catch {}
            this.peerConnection = null;
        }

        // Clean up video elements in DOM
        const localVideo = document.getElementById('localVideo');
        if (localVideo) {
            localVideo.srcObject = null;
        }
        const remoteVideo = document.getElementById('remoteVideo');
        if (remoteVideo) {
            remoteVideo.srcObject = null;
        }
        const placeholder = document.getElementById('remotePlaceholder');
        if (placeholder) {
            placeholder.classList.remove('hidden');
            placeholder.style.display = 'flex';
        }

        // Leave room and stop hub
        if (this.hubConnection) {
            try {
                if (this.roomId) await this.hubConnection.invoke("LeaveRoom", this.roomId);
                await this.hubConnection.stop();
            } catch {}
            this.hubConnection = null;
        }

        this.candidateQueue = [];
        this._isEnding = false;
        console.log("[WebRTC] Call ended cleanly.");
    },

    sendChatMessage: async function (roomId, senderName, text, isDoc) {
        if (this.hubConnection && this.hubConnection.state === signalR.HubConnectionState.Connected) {
            try {
                await this.hubConnection.invoke("SendChatMessage", roomId || this.roomId, senderName, text, isDoc);
            } catch (e) {
                console.warn("[WebRTC] Error sending chat message:", e);
            }
        }
    },

    playRingtone: function () {
        try {
            const AudioContext = window.AudioContext || window.webkitAudioContext;
            if (!AudioContext) return;
            const ctx = new AudioContext();

            const playNote = (freq, start, duration) => {
                const osc = ctx.createOscillator();
                const gain = ctx.createGain();
                osc.type = "sine";
                osc.frequency.setValueAtTime(freq, ctx.currentTime + start);
                gain.gain.setValueAtTime(0.2, ctx.currentTime + start);
                gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + start + duration);
                osc.connect(gain);
                gain.connect(ctx.destination);
                osc.start(ctx.currentTime + start);
                osc.stop(ctx.currentTime + start + duration);
            };

            playNote(587.33, 0.0, 0.45);
            playNote(880.00, 0.18, 0.65);
            playNote(587.33, 0.8, 0.45);
            playNote(880.00, 0.98, 0.85);
        } catch (e) {
            console.warn("[WebRTC] Ringtone audio failed:", e);
        }
    }
};

// Auto-clean hardware media on tab close or refresh
window.addEventListener('beforeunload', () => {
    if (window.webrtcInterop) {
        window.webrtcInterop.endCall(true);
    }
});
