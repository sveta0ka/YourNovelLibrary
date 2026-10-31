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

window.novelImages.createUrl = function (bytes, contentType) {
    return URL.createObjectURL(
        new Blob([bytes], { type: contentType })
    );
};

window.novelImages.revokeUrl = function (url) {
    if (url) {
        URL.revokeObjectURL(url);
    }
};

window.novelUi = {
    openDialog: function (element) {
        if (!element.open) {
            element.showModal();
        }
    },

    closeDialog: function (element) {
        if (element.open) {
            element.close();
        }
    }
};

window.novelAudio = (() => {
    const states = new WeakMap();
    const destroyed = new WeakSet();

    function show(state, mode, message = "") {
        state.root.dataset.musicState = mode;
        state.message.textContent = message;
    }

    async function play(state) {
        const attempt = ++state.attempt;

        try {
            await state.audio.play();

            if (!state.disposed && attempt === state.attempt) {
                show(state, "playing");
            }
        } catch (error) {
            if (state.disposed ||
                attempt !== state.attempt ||
                error.name === "AbortError") {
                return;
            }

            if (error.name === "NotAllowedError") {
                show(
                    state,
                    "blocked",
                    "Нажми кнопку, чтобы включить музыку."
                );
            } else {
                show(
                    state,
                    "error",
                    "Не удалось воспроизвести этот аудиофайл."
                );
            }
        }
    }

    function reset(state) {
        state.attempt++;

        const previousUrl = state.url;
        state.url = null;

        state.audio.pause();
        state.audio.removeAttribute("src");
        state.audio.load();

        if (previousUrl) {
            URL.revokeObjectURL(previousUrl);
        }
    }

    function getState(root) {
        let state = states.get(root);

        if (state) {
            return state;
        }

        const audio = root.querySelector("audio");
        const button = root.querySelector(".music-start-button");
        const message = root.querySelector(".music-status");

        state = {
            root,
            audio,
            button,
            message,
            url: null,
            request: -1,
            attempt: 0,
            disposed: false
        };

        state.onClick = event => {
            event.stopPropagation();
            play(state);
        };

        state.onError = () => {
            if (!state.disposed && state.url && audio.error) {
                show(
                    state,
                    "error",
                    "Не удалось воспроизвести этот аудиофайл."
                );
            }
        };

        state.onPause = () => {
            if (!state.disposed &&
                state.url &&
                audio.paused &&
                !audio.ended &&
                !audio.error) {
                show(
                    state,
                    "blocked",
                    "Музыка приостановлена. Нажми, чтобы продолжить."
                );
            }
        };

        button.addEventListener("click", state.onClick);
        audio.addEventListener("error", state.onError);
        audio.addEventListener("pause", state.onPause);

        states.set(root, state);
        return state;
    }

    return {
        setTrack(root, request, path, bytes, contentType) {
            if (destroyed.has(root) || !root.isConnected) {
                return;
            }

            const state = getState(root);

            if (request <= state.request) {
                return;
            }

            state.request = request;
            reset(state);

            if (!path || !bytes) {
                show(state, "idle");
                return;
            }

            state.url = URL.createObjectURL(
                new Blob([bytes], { type: contentType })
            );

            state.audio.src = state.url;
            state.audio.loop = true;
            state.audio.preload = "auto";

            show(state, "loading");
            play(state);
        },

        dispose(root) {
            destroyed.add(root);

            const state = states.get(root);

            if (!state) {
                return;
            }

            state.disposed = true;
            reset(state);

            state.button.removeEventListener("click", state.onClick);
            state.audio.removeEventListener("error", state.onError);
            state.audio.removeEventListener("pause", state.onPause);

            states.delete(root);
        }
    };
})();