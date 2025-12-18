(() => {
    const baseUrl = "/AdvancedFormSubmissions";
    let currentFormId = "";
    let currentPage = 1;
    let pageSize = 50;
    let totalCount = 0;
    let hiddenCols = {};
    let columnOrder = [];
    let sortColumn = "";
    let sortDirection = "asc";
    let settingsLoaded = false;
    let isInitializing = true;

    const formTopToolBar = document.getElementById("top-toolbar");
    const formPicker = document.getElementById("formPicker");
    const languagePicker = document.getElementById("languagePicker");
    const openFormBtn = document.getElementById("openFormBtn");
    const tableHead = document.getElementById("tableHead");
    const tableBody = document.getElementById("tableBody");
    const prevPageBtn = document.getElementById("prevPageBtn");
    const nextPageBtn = document.getElementById("nextPageBtn");
    const pageInfo = document.getElementById("pageInfo");
    const pageSizeSelect = document.getElementById("pageSizeSelect");
    const searchBox = document.getElementById("searchBox");
    const fromDate = document.getElementById("fromDate");
    const toDate = document.getElementById("toDate");
    const clearFilterBtn = document.getElementById("clearFilterBtn");
    const toggleColumnsBtn = document.getElementById("toggleColumnsBtn");
    const deleteSelectedBtn = document.getElementById("deleteSelectedBtn");
    const columnsPanel = document.getElementById("columnsPanel");
    const modal = document.getElementById("quickViewModal");
    const closeModalBtn = document.getElementById("closeModalBtn");
    const modalBody = document.getElementById("modalBody");
    const filtersToolbar = document.getElementById("filtersToolbar");
    const resetToolbar = document.getElementById("resetToolbar");
    const paginationBar = document.getElementById("paginationBar");
    const loadingOverlay = document.getElementById("loadingOverlay");
    const resetSettingsBtn = document.getElementById("resetSettingsBtn");
    const selectionNotice = document.getElementById("selectionNotice");
    const refreshTableBtn = document.getElementById("refreshTableBtn");
    const sitePicker = document.getElementById("sitePicker");

    const exportMenuBtn = document.getElementById("exportMenuBtn");
    const exportMenu = document.getElementById("exportMenu");

    exportMenuBtn.addEventListener("click", (e) => {
        e.stopPropagation();
        exportMenu.classList.toggle("hidden");
    });

    document.addEventListener("click", () => exportMenu.classList.add("hidden"));

    // Handle export type selection
    exportMenu.querySelectorAll("button").forEach(btn => {
        btn.addEventListener("click", async () => {
            const type = btn.getAttribute("data-type");
            exportMenu.classList.add("hidden");
            await handleExport(type);
        });
    });

    resetSettingsBtn.addEventListener("click", async () => {
        if (!currentFormId) return alert("Select a form first");
        if (!confirm("Reset column order, visibility, and sort settings for this form?")) return;
        const lang = getSelectedLanguage();
        await fetch(`${baseUrl}/ClearFormCache`, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "RequestVerificationToken": document.querySelector('input[name="__RequestVerificationToken"]').value
            },
            body: JSON.stringify({ formId: currentFormId, language: lang })
        });
        hiddenCols = {};
        columnOrder = [];
        sortColumn = "";
        sortDirection = "asc";
        loadSubmissions();
    });

    sitePicker.addEventListener("change", () => {
        currentFormId = "";
        formPicker.innerHTML = '<option value="">Select a form</option>';
        tableBody.innerHTML = "";
        filtersToolbar.classList.add("hidden");
        resetToolbar.classList.add("hidden");
        paginationBar.classList.add("hidden");
        tableHead.innerHTML = "";
        loadForms();
    });

    function showLoading() {
        loadingOverlay.classList.remove("hidden");
    }
    function hideLoading() {
        loadingOverlay.classList.add("hidden");
    }
    function getSelectedLanguage() {
        return languagePicker?.value || "en";
    }

    async function loadSettings() {
        if (!currentFormId) return;
        isInitializing = true;
        settingsLoaded = false;

        try {
            const lang = getSelectedLanguage();
            const res = await fetch(`${baseUrl}/GetFormSettings?formId=${encodeURIComponent(currentFormId)}&language=${encodeURIComponent(lang)}`);
            const data = await res.json();

            if (data.status && data.settings) {
                hiddenCols = data.settings.hiddenCols || {};
                columnOrder = data.settings.columnOrder || [];
            } else {
                hiddenCols = {};
                columnOrder = [];
            }
        } catch (e) {
            console.warn("Failed to load form settings:", e);
        } finally {
            settingsLoaded = true;
        }
    }

    async function saveSettings(force = false) {
        if (isInitializing && !force) return;
        if (!currentFormId) return;

        const lang = getSelectedLanguage();

        try {
            await fetch(`${baseUrl}/SaveFormSettings`, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "RequestVerificationToken": document.querySelector('input[name="__RequestVerificationToken"]').value
                },
                body: JSON.stringify({
                    formId: currentFormId,
                    language: lang,
                    hiddenCols,
                    columnOrder
                })
            });
        } catch (e) {
            console.warn("Failed to save form settings:", e);
        }
    }

    async function fetchJson(url) {
        const res = await fetch(url);
        if (!res.ok) throw new Error(await res.text());
        return await res.json();
    }

    async function loadLanguages() {
        try {
            const result = await fetchJson(`${baseUrl}/GetLanguages`);
            if (!result.status) return alert("Error loading languages: " + result.message);

            const langs = result.data || [];
            languagePicker.innerHTML = langs.map(l => `<option value="${l.languageID}">${l.name}</option>`).join("");
            const savedLang = localStorage.getItem("formSelectedLanguage");
            const defaultLang = langs.find(l => l.languageID === savedLang) ||
                langs.find(l => l.languageID.toLowerCase() === "en") || langs[0];
            if (defaultLang) languagePicker.value = defaultLang.languageID;
        } catch (err) {
            console.error("Error loading languages:", err);
        }
    }

    async function loadForms() {
        showLoading();
        try {
            const language = getSelectedLanguage();
            const site = sitePicker?.value || "";
            const result = await fetchJson(`${baseUrl}/GetForms?language=${encodeURIComponent(language)}&site=${encodeURIComponent(site)}`);
            if (!result.status) return alert("Error loading forms: " + result.message);

            const forms = result.data || [];
            formPicker.innerHTML = '<option value="">Select a form</option>' +
                forms.map(f => `<option value="${f.id}" data-contentid="${f.contentId}">${f.name}</option>`).join("");
        } finally { hideLoading(); }
    }

    async function loadSubmissions() {
        if (!currentFormId) return;
        showLoading();
        tableHead.innerHTML = "";
        tableBody.innerHTML = "<tr><td class=\"tbl-message\">Loading...</td></tr>";
        filtersToolbar.classList.add("hidden");
        resetToolbar.classList.add("hidden");
        paginationBar.classList.add("hidden");

        try {
            const params = buildQueryParams({ page: currentPage, pageSize });
            const lang = getSelectedLanguage();
            const result = await fetchJson(`${baseUrl}/GetSubmissions?${params}&language=${encodeURIComponent(lang)}`);
            if (!result.status) {
                tableBody.innerHTML = `<tr><td class="tbl-message">Error: ${result.message}</td></tr>`;
                return;
            }

            totalCount = result.total;
            if (!result.items?.length) {
                tableBody.innerHTML = "<tr><td class=\"tbl-message\">No submissions found</td></tr>";
                return;
            }

            filtersToolbar.classList.remove("hidden");
            resetToolbar.classList.remove("hidden");
            paginationBar.classList.remove("hidden");

            renderTable(result.items || []);
            applyEllipsisTooltips();
        } catch (err) {
            console.error("Error loading submissions:", err);
            tableBody.innerHTML = `<tr><td class=\"tbl-message\">Error: ${err.message}</td></tr>`;
        } finally { hideLoading(); }
    }

    async function loadSites() {
        try {
            const result = await fetchJson(`${baseUrl}/GetSites`);
            if (!result.status) return alert("Error loading sites: " + result.message);

            const sites = result.data || [];
            sitePicker.innerHTML = '<option value="">All Sites</option>' +
                sites.map(s => `<option value="${s.id}">${s.displayName}</option>`).join("");
        } catch (err) {
            console.error("Error loading sites:", err);
        }
    }

    function buildQueryParams(overrides = {}) {
        const params = new URLSearchParams({
            formId: currentFormId,
            page: overrides.page ?? currentPage,
            pageSize: overrides.pageSize ?? pageSize,
            q: searchBox.value || ""
        });
        if (fromDate.value) params.set("from", fromDate.value);
        if (toDate.value) params.set("to", toDate.value);
        return params.toString();
    }

    function renderTable(items) {
        window.__lastLoadedItems = items;
        tableBody.innerHTML = "";
        if (!items.length) {
            tableBody.innerHTML = "<tr><td>No submissions found</td></tr>";
            return;
        }

        const dynamicKeys = new Set();
        items.forEach(i => Object.keys(i.fields).forEach(k => dynamicKeys.add(k)));
        let columns = [...Array.from(dynamicKeys)];

        if (columnOrder.length && columnOrder.every(c => columns.includes(c))) {
            const ordered = columnOrder.filter(c => columns.includes(c));
            const remaining = columns.filter(c => !columnOrder.includes(c));
            columns = [...ordered, ...remaining];
        } else {
            columnOrder = [...columns];
            // Prevent saving during load or re-selection
            if (!isInitializing) saveSettings();
        }

        // header with select all
        tableHead.innerHTML = `
            <th style="width:30px;"><input type="checkbox" id="selectAllCheckbox" title="Select all" /></th>
            <th title="Overview" style="width:50px;">Overview</th>` +

            columns.map(col => {
                if (hiddenCols[col])
                    return "";

                if (col === "Finalized") {
                    return `<th title="${escapeHtml(col)}" style="width:50px">${escapeHtml(col)}</th>`;
                }

                if (col === "Open") {
                    return `<th title="View" style="width:30px">View</th>`;
                }

                const sortIndicator = sortColumn === col
                    ? (sortDirection === "asc" ? " ▲" : " ▼")
                    : "";

                return `
            <th data-col="${col}"
                draggable="true"                
                title="${escapeHtml(col)}"
                class="sortable draggable">
                ${escapeHtml(col)}${sortIndicator}
            </th>`;
            }).join("");

        // sort
        if (sortColumn) {
            items.sort((a, b) => {
                const av = a.fields[sortColumn] ?? "";
                const bv = b.fields[sortColumn] ?? "";
                return sortDirection === "asc" ? String(av).localeCompare(String(bv)) : String(bv).localeCompare(String(av));
            });
        }

        // rows with checkbox
        items.forEach(i => {
            const tr = document.createElement("tr");
            tr.innerHTML = `
        <td><input type="checkbox" class="row-checkbox" data-id="${i.submissionId}" /></td>
        <td><button class="view-btn" data-id="${i.submissionId}">   
            <i class="fa-solid fa-rectangle-list fa-lg"></i>
            </button>
        </td>
        ${columns.map(c => hiddenCols[c] ? "" : `<td>${getValue(i, c)}</td>`).join("")}`;
            tableBody.appendChild(tr);
        });

        const totalPages = Math.ceil(totalCount / pageSize);
        pageInfo.textContent = `Page ${currentPage} of ${totalPages} (${totalCount} total)`;

        columnsPanel.innerHTML =
            `<label style="font-weight:bold; display:block; margin-bottom:6px;">
                <input type="checkbox" id="toggleAllColumns" /> Select / Unselect All
             </label>` +
            columns
                .filter(c => c !== "Open")
                .map(c =>
                    `<label><input type="checkbox" ${hiddenCols[c] ? "" : "checked"} data-col="${c}"/> ${c}</label>`
                ).join("<br/>");

        const toggleAll = document.getElementById("toggleAllColumns");

        if (toggleAll) {
            toggleAll.addEventListener("change", e => {
                const check = e.target.checked;

                // Toggle all child checkboxes
                columnsPanel.querySelectorAll("input[type='checkbox'][data-col]").forEach(cb => {
                    cb.checked = check;
                    hiddenCols[cb.getAttribute("data-col")] = !check;
                });

                saveSettings();
                loadSubmissions();
            });

            // Ensure initial checked state matches column visibility
            const anyHidden = columns.some(c => hiddenCols[c]);
            toggleAll.checked = !anyHidden;
        }

        applyEllipsisTooltips();
        applyHeaderTooltips();
        bindHeaderSorting(items);
        enableColumnDragging(columns);
        bindCheckboxEvents();

        if (settingsLoaded) {
            // release lock once the first render is complete
            isInitializing = false;
        }
    }

    function bindCheckboxEvents() {
        const selectAll = document.getElementById("selectAllCheckbox");
        const checkboxes = document.querySelectorAll(".row-checkbox");

        if (!selectAll) return;

        selectAll.addEventListener("change", () => {
            checkboxes.forEach(cb => cb.checked = selectAll.checked);
            updateSelectionNotice();
        });

        checkboxes.forEach(cb => cb.addEventListener("change", updateSelectionNotice));
        updateSelectionNotice();
    }

    function updateSelectionNotice() {
        const checkboxes = document.querySelectorAll(".row-checkbox");
        const checked = Array.from(checkboxes).filter(cb => cb.checked);
        const total = checkboxes.length;
        const allSelected = checked.length === total && total > 0;

        // Enable or disable delete button
        deleteSelectedBtn.disabled = checked.length === 0;

        if (checked.length === 0) {
            selectionNotice.classList.add("hidden");
            return;
        }

        // Show notification
        selectionNotice.classList.remove("hidden");

        if (allSelected) {
            selectionNotice.innerHTML = `
      All&nbsp;<strong>${total}</strong>&nbsp;record(s) are selected.
      <a id="clearSelectionLink">Clear selection</a>
    `;
            document.getElementById("clearSelectionLink").addEventListener("click", () => {
                checkboxes.forEach(cb => cb.checked = false);
                document.getElementById("selectAllCheckbox").checked = false;
                updateSelectionNotice();
            });
        } else {
            selectionNotice.innerHTML = `
      All&nbsp;<strong>${checked.length}</strong>&nbsp;record(s) are selected.
      <a id="selectAllLink">Select all ${total} records</a>
    `;
            document.getElementById("selectAllLink").addEventListener("click", () => {
                checkboxes.forEach(cb => cb.checked = true);
                document.getElementById("selectAllCheckbox").checked = true;
                updateSelectionNotice();
            });
        }
    }

    async function deleteSelected() {
        const selectedIds = Array.from(document.querySelectorAll(".row-checkbox:checked"))
            .map(cb => cb.dataset.id);
        if (!selectedIds.length) return;

        if (!confirm("Do you want to delete the selected items? This action cannot be undone.")) return;

        try {
            showLoading();
            const lang = getSelectedLanguage();
            const response = await fetch(`${baseUrl}/DeleteSubmissions`, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "RequestVerificationToken": document.querySelector('input[name="__RequestVerificationToken"]').value
                },
                body: JSON.stringify({
                    formId: currentFormId,
                    language: lang,
                    submissionIds: selectedIds
                })
            });

            const result = await response.json();
            if (!result.status) return alert("Error: " + result.message);

            alert(`${selectedIds.length} submission(s) deleted successfully.`);
            loadSubmissions();
        } catch (err) {
            alert("Error deleting submissions: " + err.message);
        } finally { hideLoading(); }
    }

    deleteSelectedBtn.addEventListener("click", deleteSelected);

    function getValue(item, key, asText = false) {
        const value = item.fields[key];

        // Submitted from: expects an object with name and link
        if (key === "Submitted from" && value && typeof value === "object") {
            const name = value.name || "";
            const link = value.link || "";
            if (link) {
                const label = name || link;
                if (asText) {
                    return `${label} (${link})`;
                }
                return `<a href="${escapeHtml(link)}" target="_blank" rel="noopener">${escapeHtml(label)}</a>`;
            }
            return asText ? name : escapeHtml(name);
        }

        // Multiple file references separated by "|"
        if (typeof value === "string" && value.includes("/") && value.includes("#@")) {
            const parts = value.split("|").map(p => p.trim()).filter(p => p.length > 0);

            const links = parts.map(itemPart => {
                if (!itemPart.startsWith("/") || !itemPart.includes("#@")) {
                    return asText ? itemPart : escapeHtml(itemPart);
                }

                // Split at first #@
                const idx = itemPart.indexOf("#@");
                let urlPart = itemPart.substring(0, idx).trim();
                let namePart = itemPart.substring(idx + 2).trim();

                // Remove trailing ] or whitespace
                urlPart = urlPart.replace(/[\]\s]+$/g, "");
                namePart = namePart.replace(/[\]\s]+$/g, "");

                const cleanUrl = urlPart;
                const displayName = namePart || cleanUrl.split("/").pop();

                if (asText) {
                    // Example: file.pdf (/contentassets/abc/file.pdf)
                    return `${displayName} (${cleanUrl})`;
                }

                return `<a href="${escapeHtml(cleanUrl)}" title="${escapeHtml(displayName)}" target="_blank" rel="noopener">${escapeHtml(displayName)}</a>`;
            });

            return asText ? links.join(", ") : links.join("<br>");
        }

        // Time field: formatted date and time
        if (key === "Time" && value) {
            const dt = new Date(value);
            if (!isNaN(dt)) {
                const pad = n => n.toString().padStart(2, "0");
                return `${dt.getFullYear()}-${pad(dt.getMonth() + 1)}-${pad(dt.getDate())}, ${pad(dt.getHours())}:${pad(dt.getMinutes())}:${pad(dt.getSeconds())}`;
            }
        }

        // Finalized: boolean checkmark
        if (key === "Finalized") {
            return value === "True" || value === true ? (asText ? "✔️" : "✔️") : "";
        }

        // Open: always a link
        if (key === "Open" && value) {
            const safeUrl = escapeHtml(value);
            if (asText) {
                return safeUrl;
            }
            return `<a href="${safeUrl}" target="_blank" rel="noopener">
                    <i class="fa-solid fa-arrow-up-right-from-square fa-lg"></i>
                </a>`;
        }

        // Default: safe text output
        return asText ? (value ?? "") : escapeHtml(value ?? "");
    }

    function escapeHtml(text) {
        return text == null ? "" : String(text)
            .replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#039;" }[c]));
    }

    async function openQuickView(id) {
        const lang = getSelectedLanguage();
        const result = await fetchJson(
            `${baseUrl}/GetSubmissionDetail?submissionId=${encodeURIComponent(id)}&formId=${encodeURIComponent(currentFormId)}&language=${encodeURIComponent(lang)}`
        );
        if (!result.status) return alert(result.message);

        const data = result.data;
        modalBody.innerHTML = `
    <p><strong>ID:</strong> ${escapeHtml(data.submissionId)}</p>
    <hr>
    <table class="fields-table">
      ${Object.entries(data.fields)
                .filter(c => c !== "Open")
                .map(([key, value]) => `<tr>
          <td><b>${escapeHtml(key)}</b></td>
          <td>${getValue({ fields: { [key]: value } }, key)}</td>
        </tr>`)
                .join("")}
    </table>`;
        modal.classList.remove("hidden");
    }

    function applyEllipsisTooltips() {
        document.querySelectorAll(".submissions-table td").forEach(td => {
            if (td.scrollWidth > td.clientWidth) td.title = td.textContent.trim();
            else td.removeAttribute("title");
        });
    }

    function applyHeaderTooltips() {
        document.querySelectorAll(".submissions-table th[title]").forEach(th => {
            if (th.scrollWidth > th.clientWidth) th.title = th.textContent.trim();
            else th.removeAttribute("title");
        });
    }

    function bindHeaderSorting(items) {
        document.querySelectorAll(".submissions-table th.sortable").forEach(th => {
            th.addEventListener("click", () => {
                const col = th.getAttribute("data-col");
                if (!col) return;
                if (sortColumn === col) sortDirection = sortDirection === "asc" ? "desc" : "asc";
                else { sortColumn = col; sortDirection = "asc"; }
                saveSettings();
                renderTable(items);
            });
        });
    }

    function enableColumnDragging(columns) {
        const headers = document.querySelectorAll(".submissions-table th.draggable");
        let draggedCol = null;
        headers.forEach(th => {
            th.addEventListener("dragstart", e => {
                draggedCol = th.getAttribute("data-col");
                e.dataTransfer.effectAllowed = "move";
                th.classList.add("dragging");
            });
            th.addEventListener("dragover", e => {
                e.preventDefault();
                e.dataTransfer.dropEffect = "move";
                th.classList.add("drag-over");
            });
            th.addEventListener("dragleave", () => th.classList.remove("drag-over"));
            th.addEventListener("drop", e => {
                e.preventDefault();
                th.classList.remove("drag-over");
                const targetCol = th.getAttribute("data-col");
                if (draggedCol && targetCol && draggedCol !== targetCol) {
                    const fromIndex = columns.indexOf(draggedCol);
                    const toIndex = columns.indexOf(targetCol);
                    columns.splice(fromIndex, 1);
                    columns.splice(toIndex, 0, draggedCol);
                    columnOrder = [...columns];
                    saveSettings();
                    renderTable([...window.__lastLoadedItems]);
                }
            });
            th.addEventListener("dragend", () => th.classList.remove("dragging"));
        });
    }

    function debounce(fn, delay) {
        let timer;
        return (...args) => { clearTimeout(timer); timer = setTimeout(() => fn.apply(this, args), delay); };
    }

    async function handleExport(type) {
        if (!currentFormId) return alert("Select a form first");

        showLoading();
        try {
            // Get all visible items or only selected ones
            const allItems = window.__lastLoadedItems || [];
            const selectedIds = Array.from(document.querySelectorAll(".row-checkbox:checked"))
                .map(cb => cb.dataset.id);

            let itemsToExport = selectedIds.length
                ? allItems.filter(i => selectedIds.includes(i.submissionId))
                : allItems;

            if (!itemsToExport.length) {
                alert("No records to export.");
                return;
            }

            const data = buildTableDataForExport(itemsToExport);
            let content = "";
            let mime = "text/plain";
            let ext = type;

            switch (type) {
                case "csv":
                    content = buildCsv(data);
                    mime = "text/csv;charset=utf-8;";
                    break;
                case "json":
                    content = JSON.stringify(data, null, 2);
                    mime = "application/json";
                    break;
                case "xml":
                    content = buildXml(data);
                    mime = "application/xml";
                    break;
                default:
                    alert("Unsupported export type.");
                    return;
            }

            const blob = new Blob([content], { type: mime });
            const link = document.createElement("a");
            link.href = URL.createObjectURL(blob);

            const formName = (formPicker.options[formPicker.selectedIndex]?.text || "form-submissions");

            link.download = `${formName}.${ext}`;
            link.click();
        } catch (err) {
            alert("Export failed: " + err.message);
        } finally {
            hideLoading();
        }
    }

    function buildTableDataForExport(items) {
        const visibleCols = columnOrder.filter(c => !hiddenCols[c]);
        return items.map(i => {
            const obj = { SubmissionId: i.submissionId };
            for (const col of visibleCols) {
                obj[col] = getValue(i, col, true); // <-- use text mode
            }
            return obj;
        });
    }

    function buildCsv(data) {
        if (!Array.isArray(data) || data.length === 0) {
            return "";
        }

        const headers = Object.keys(data[0] || {});

        const escapeCsvValue = (value) => {
            if (value == null) return ""; // null or undefined → empty cell
            let str = String(value).trim();

            // Normalize line endings
            str = str.replace(/\r?\n/g, " ");

            // Escape double quotes by doubling them
            str = str.replace(/"/g, '""');

            // Always wrap in quotes if contains comma, quote, or whitespace
            if (/[",\n\r]/.test(str) || /^\s|\s$/.test(str)) {
                str = `"${str}"`;
            }

            return str;
        };

        const normalizeFinalized = (val) => {
            if (val == null) return "";
            const s = String(val).toLowerCase().trim();
            return s ? "Yes" : "";
        };

        const headerLine = headers.map(escapeCsvValue).join(",");

        const rowLines = data.map(row =>
            headers.map(h => {
                let cellValue = row[h];
                if (h.toLowerCase() === "finalized") {
                    cellValue = normalizeFinalized(cellValue);
                }
                return escapeCsvValue(cellValue);
            }).join(",")
        );

        // CRLF for best cross-platform support
        return [headerLine, ...rowLines].join("\r\n");
    }


    function buildXml(data) {
        return (
            `<?xml version="1.0" encoding="UTF-8"?>\n<Submissions>\n` +
            data.map(row => {
                const fields = Object.entries(row)
                    .map(([k, v]) => `  <${k}>${escapeXml(v)}</${k}>`)
                    .join("\n");
                return `<Submission>\n${fields}\n</Submission>`;
            }).join("\n") +
            "\n</Submissions>"
        );
    }

    function escapeXml(value) {
        return String(value ?? "").replace(/[<>&'"]/g, c => ({
            "<": "&lt;",
            ">": "&gt;",
            "&": "&amp;",
            "'": "&apos;",
            '"': "&quot;"
        }[c]));
    }

    formPicker.addEventListener("change", async e => {
        currentFormId = e.target.value;
        openFormBtn.disabled = !currentFormId;
        currentPage = 1;

        isInitializing = true; // lock saves during reload
        await loadSettings();
        await loadSubmissions();
        isInitializing = false; // unlock after render
    });

    openFormBtn.addEventListener("click", () => {
        const selectedOption = formPicker.options[formPicker.selectedIndex];
        const contentId = selectedOption?.dataset.contentid;
        if (contentId)
            window.open(`/EPiServer/CMS/#context=epi.cms.contentdata:///${contentId}`, "_blank");
    });

    pageSizeSelect.addEventListener("change", e => {
        pageSize = parseInt(e.target.value, 10); currentPage = 1; loadSubmissions();
    });

    prevPageBtn.addEventListener("click", () => { if (currentPage > 1) { currentPage--; loadSubmissions(); } });

    nextPageBtn.addEventListener("click", () => {
        const totalPages = Math.ceil(totalCount / pageSize);
        if (currentPage < totalPages) { currentPage++; loadSubmissions(); }
    });

    searchBox.addEventListener("input", debounce(() => { currentPage = 1; loadSubmissions(); }, 500));

    clearFilterBtn.addEventListener("click", () => { searchBox.value = ""; fromDate.value = ""; toDate.value = ""; currentPage = 1; loadSubmissions(); });
    refreshTableBtn.addEventListener("click", async () => {
        if (!currentFormId) {
            alert("Please select a form first");
            return;
        }
        showLoading();
        try {
            currentPage = 1;
            await loadSubmissions();
        } catch (err) {
            alert("Failed to refresh submissions: " + err.message);
        } finally {
            hideLoading();
        }
    });


    toggleColumnsBtn.addEventListener("click", () => columnsPanel.classList.toggle("hidden"));

    columnsPanel.addEventListener("change", e => {
        const col = e.target.getAttribute("data-col");
        hiddenCols[col] = !e.target.checked;
        saveSettings();
        loadSubmissions();
    });

    tableBody.addEventListener("click", e => {
        const btn = e.target.closest(".view-btn");
        if (btn) openQuickView(btn.dataset.id);
    });

    closeModalBtn.addEventListener("click", () => modal.classList.add("hidden"));

    languagePicker.addEventListener("change", () => {
        localStorage.setItem("formSelectedLanguage", languagePicker.value);
        currentFormId = "";
        formPicker.innerHTML = '<option value="">Select a form</option>';
        tableBody.innerHTML = "";

        filtersToolbar.classList.add("hidden");
        resetToolbar.classList.add("hidden");
        paginationBar.classList.add("hidden");
        tableHead.innerHTML = "";
        loadForms();
    });

    (async () => {
        await loadSites();
        await loadLanguages();
        await loadForms();
        const cfg = window.FormSubmissionsConfig || {};
        const params = new URLSearchParams(window.location.search);

        const lang = cfg.preselectedLanguage || params.get("language");
        if (lang) {
            const langOption = Array.from(languagePicker.options).find(opt =>
                opt.value.toLowerCase() === lang.toLowerCase()
            );
            if (langOption) {
                languagePicker.value = langOption.value;
            }
        }

        const formId = cfg.preselectedFormGuid || params.get("formGuid") || params.get("id");
        if (formId) {
            formTopToolBar.classList.add("hidden");
            currentFormId = formId;
            currentPage = 1;
            loadSettings();
            loadSubmissions();
        }
    })();
})();