mergeInto(LibraryManager.library, {
    GetTelegramUserData: function () {
        var userData = "";
        if (window.Telegram && window.Telegram.WebApp && window.Telegram.WebApp.initDataUnsafe && window.Telegram.WebApp.initDataUnsafe.user) {
            userData = JSON.stringify(window.Telegram.WebApp.initDataUnsafe.user);
        } else {
            userData = JSON.stringify({id: 0, first_name: "Web User", username: "web_user"});
        }
        var bufferSize = lengthBytesUTF8(userData) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(userData, buffer, bufferSize);
        return buffer;
    }
});