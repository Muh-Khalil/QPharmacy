window.adminExport = {
    downloadFileFromText: function (filename, content, mimeType) {
        mimeType = mimeType || 'text/csv;charset=utf-8;';
        const blob = new Blob([content], { type: mimeType });
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = filename;
        document.body.appendChild(anchor);
        anchor.click();
        document.body.removeChild(anchor);
        URL.revokeObjectURL(url);
    }
};
