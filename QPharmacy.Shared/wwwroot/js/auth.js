window.checkBiometricSupport = async function () {
    if (window.Platform && window.Platform.isAndroid) {
        // Android biometric check
        return await window.BiometricPlugin.isAvailable();
    } else if (window.Platform && window.Platform.isIOS) {
        // iOS FaceID/TouchID check
        return await window.AppleID.isSupported();
    }
    return false;
};

window.showToast = function (title, message, type) {
    // Implementation using your preferred toast library
    if (window.Toast) {
        window.Toast.show(message, {
            title: title,
            type: type,
            duration: 3000
        });
    } else {
        alert(`${title}: ${message}`);
    }
};