// 檔案下載輔助函式
// 觸發瀏覽器下載 Base64 編碼的檔案

window.downloadFileFromBase64 = function (fileName, contentType, base64Content) {
    // 建立 Data URI
    const dataUri = `data:${contentType};base64,${base64Content}`;

    // 建立臨時連結元素
    const link = document.createElement('a');
    link.href = dataUri;
    link.download = fileName;

    // 觸發下載
    document.body.appendChild(link);
    link.click();

    // 清理
    document.body.removeChild(link);
};

window.downloadFileFromBytes = function (fileName, contentType, byteArray) {
    // 建立 Blob
    const blob = new Blob([byteArray], { type: contentType });

    // 建立 Object URL
    const url = window.URL.createObjectURL(blob);

    // 建立臨時連結元素
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;

    // 觸發下載
    document.body.appendChild(link);
    link.click();

    // 清理
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
};
