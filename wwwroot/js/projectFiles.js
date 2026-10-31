window.novelFiles = {
    download: async function (title, streamReference) {
        const buffer = await streamReference.arrayBuffer();

        const blob = new Blob(
            [buffer],
            { type: "application/zip" }
        );

        const safeTitle = title
            .replace(/[<>:"/\\|?*\u0000-\u001F]/g, "_")
            .trim() || "novel";

        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");

        link.href = url;
        link.download = `YourNovelLibrary-${safeTitle}.ynl`;

        document.body.appendChild(link);
        link.click();
        link.remove();

        setTimeout(() => URL.revokeObjectURL(url), 60000);
    }
};

window.novelImages = {
    thumbnail: async function (bytes) {
        const url = URL.createObjectURL(new Blob([bytes]));

        try {
            const image = new Image();

            await new Promise((resolve, reject) => {
                image.onload = () => resolve();
                image.onerror = () =>
                    reject(new Error("Не удалось открыть изображение."));

                image.src = url;
            });

            const scale = Math.min(
                1,
                240 / Math.max(image.naturalWidth, image.naturalHeight)
            );

            const canvas = document.createElement("canvas");

            canvas.width = Math.max(
                1, Math.round(image.naturalWidth * scale)
            );

            canvas.height = Math.max(
                1, Math.round(image.naturalHeight * scale)
            );

            const context = canvas.getContext("2d");

            if (!context) {
                throw new Error("Не удалось создать миниатюру.");
            }

            context.drawImage(image, 0, 0, canvas.width, canvas.height);

            return canvas.toDataURL("image/png");
        } finally {
            URL.revokeObjectURL(url);
        }
    }
};