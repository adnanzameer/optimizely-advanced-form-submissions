document.addEventListener("DOMContentLoaded", function () {
    // Detect form ID from the script tag itself
    const currentScript = document.currentScript || document.querySelector('script[data-form-id]');
    const formId = currentScript?.dataset.formId;

    // Disable form fields if found
    if (formId) {
        const form = document.getElementById(formId);
        if (form) {
            form.querySelectorAll("input, select, textarea, button, adgm-button").forEach(el => el.disabled = true);
            form.querySelectorAll(".input-with-flag .dropdown").forEach(function (dropdown) {
                dropdown.style.display = "none";
            });

            document.querySelectorAll(".FormFileUpload").forEach(block => {
                const uploadBtn = block.querySelector(".upload-btn");
                if (uploadBtn)
                    uploadBtn.style.display = "none";
            });
        }
    }

    // Hydrate file upload links
    document.querySelectorAll(".FormFileUpload__Input[title*='<a']").forEach(function (input) {
        if (input.dataset.hydrated === "true") return;
        input.dataset.hydrated = "true";

        try {
            const decoded = input.title;
            const container = input.closest(".Form__Element");
            if (!container) return;

            const linkWrapper = document.createElement("div");
            linkWrapper.className = "hydrated-file-label";
            linkWrapper.innerHTML = decoded;

            container.insertBefore(linkWrapper, input);
            input.style.display = "none";
        } catch (err) {
            console.warn("Failed to decode file link:", err);
        }
    });
});
