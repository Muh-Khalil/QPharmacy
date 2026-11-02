using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace QPharmacy.Web.Hubs
{
    public class ConsultationHub : Hub
    {
        // Join a consultation room based on PatientId
        public async Task JoinRoom(string roomId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
            await Clients.OthersInGroup(roomId).SendAsync("UserJoined");
        }

        // Leave the consultation room
        public async Task LeaveRoom(string roomId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
        }

        // WebRTC Signaling: Send Offer
        public async Task SendOffer(string offer, string roomId)
        {
            await Clients.OthersInGroup(roomId).SendAsync("ReceiveOffer", offer);
        }

        // WebRTC Signaling: Send Answer
        public async Task SendAnswer(string answer, string roomId)
        {
            await Clients.OthersInGroup(roomId).SendAsync("ReceiveAnswer", answer);
        }

        // WebRTC Signaling: Send ICE Candidate
        public async Task SendIceCandidate(string candidate, string roomId)
        {
            await Clients.OthersInGroup(roomId).SendAsync("ReceiveIceCandidate", candidate);
        }

        // Real-time Chat Messaging
        public async Task SendChatMessage(string roomId, string senderName, string messageText, bool isDoctor, string? senderId = null)
        {
            var timestamp = System.DateTime.UtcNow.ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture);
            await Clients.Group(roomId).SendAsync("ReceiveChatMessage", senderName, messageText, isDoctor, timestamp, senderId);
        }

        // Call status events
        public async Task NotifyCallEnded(string roomId)
        {
            if (!string.IsNullOrWhiteSpace(roomId))
            {
                await Clients.Group(roomId).SendAsync("CallEnded");
                await Clients.All.SendAsync("ConsultationEndedInRoom", roomId);
            }
        }

        // WebRTC Signaling: Client is ready to receive offer
        public async Task UserReady(string roomId)
        {
            if (!string.IsNullOrWhiteSpace(roomId))
            {
                await Clients.OthersInGroup(roomId).SendAsync("UserReady");
            }
        }

        // Real-time media toggle sync (Camera on/off, Mic mute/unmute)
        public async Task SendMediaStatus(string roomId, bool isCameraOff, bool isMicMuted)
        {
            if (!string.IsNullOrWhiteSpace(roomId))
            {
                await Clients.OthersInGroup(roomId).SendAsync("ReceiveRemoteMediaStatus", isCameraOff, isMicMuted);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // LOBBY / QUEUE FLOW
        // ─────────────────────────────────────────────────────────────────

        // Doctor joins their lobby notification group on the Consultations page
        public async Task DoctorJoinLobby(string doctorLobbyRoomId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "lobby-doctors");
            if (!string.IsNullOrWhiteSpace(doctorLobbyRoomId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, doctorLobbyRoomId);
            }
        }

        // Patient joins personal return room
        public async Task JoinPatientLobbyRoom(string patientRoomId)
        {
            if (!string.IsNullOrWhiteSpace(patientRoomId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, patientRoomId);
            }
        }

        // Patient joins lobby waiting: notifies doctor and joins personal return room
        public async Task PatientJoinedLobby(string doctorLobbyRoomId, string patientRoomId, string patientName, string patientId, string appointmentId, string scheduledTime)
        {
            // Join own personal room so doctor can send acceptance back
            if (!string.IsNullOrWhiteSpace(patientRoomId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, patientRoomId);
            }

            // Notify specific doctor lobby if given
            if (!string.IsNullOrWhiteSpace(doctorLobbyRoomId) && doctorLobbyRoomId != "lobby-doctor-default")
            {
                await Clients.Group(doctorLobbyRoomId).SendAsync("PatientIsWaiting", patientName, patientId, appointmentId, scheduledTime);
            }

            // Broadcast to all doctors in lobby view
            await Clients.Group("lobby-doctors").SendAsync("PatientIsWaiting", patientName, patientId, appointmentId, scheduledTime);
        }

        // Patient leaves the lobby (e.g. cancelled or navigated away)
        public async Task PatientLeftLobby(string doctorLobbyRoomId, string patientId)
        {
            if (!string.IsNullOrWhiteSpace(doctorLobbyRoomId))
            {
                await Clients.Group(doctorLobbyRoomId).SendAsync("PatientLeftLobby", patientId);
            }
            await Clients.Group("lobby-doctors").SendAsync("PatientLeftLobby", patientId);
        }

        // Doctor accepts a patient — notify patient via their personal room
        public async Task DoctorAcceptedPatient(string patientRoomId, string doctorName, string appointmentId)
        {
            await Clients.Group(patientRoomId).SendAsync("DoctorJoined", doctorName, appointmentId);
        }

        // Doctor joins room first — notify patient waiting in lobby
        public async Task DoctorWaitingInRoom(string patientRoomId, string doctorName, string appointmentId)
        {
            await Clients.Group(patientRoomId).SendAsync("DoctorWaitingInRoom", doctorName, appointmentId);
        }
    }
}
