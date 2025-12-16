## 🚀 Advanced Form Submissions Add-on for Optimizely Forms (CMS 12)

This add-on is designed as an alternative of the standard Optimizely Forms submissions view with a more powerful, user-friendly dashboard optimized for efficiency and data management in Optimizely CMS 12 (.NET Core).

---

### ✨ What is this Add-on?

This add-on addresses common pain points with the default Optimizely Forms submissions view, such as the lack of user preference persistence and limited display options. It provides a new, centralized, and highly optimized dashboard built to make managing form data easier.

### 🌟 Key Features for Editors and Administrators

The new dashboard transforms your workflow for handling form data:

#### 1. Personalized and Persistent Dashboard
* **Customizable Grid:** You have full control over the submissions table. You can **reorder** columns and **hide** any fields you don't need (e.g., system fields or irrelevant data).
* **Settings Persistence:** Your layout choices (hidden columns, column order) are saved automatically (likely using local storage) and are specific to you, the user. The next time you open the dashboard, your preferred view will load instantly.
* **Optimized Performance:** The underlying system is built for speed, using paged API calls to handle even very large forms efficiently.

#### 2. Advanced Data Filtering and Search
The dashboard features dedicated controls for quickly narrowing down your submissions:
* **Contextual Filtering:** Filter data by **Site**, **Language**, and **Form** block.
* **Search by Content:** Use a dedicated search box to find specific entries across submission data.
* **Date Range Filtering:** Easily select a **From Date** and **To Date** to view submissions within a specific period.

#### 3. Management and Export Tools
* **Export Options:** Export your filtered submission data instantly into various formats:
    * **CSV** (Comma Separated Values)
    * **XML** (Extensible Markup Language)
    * **JSON** (JavaScript Object Notation)
* **Delete Submissions:** Securely delete selected or filtered form entries from the system.
* **Direct Form Link:** If you need to view or edit the original form definition, a button provides a **one-click link** to the Form Container Block in the CMS editing view.

#### 4. Enhanced Submission Data Display
The add-on includes specialized handling to make certain data types more useful during review (seen in the Quick View modal):
* **Clickable File Uploads:** For submissions that included a **File Upload Element**, the path to the uploaded file is automatically converted into a **clickable HTML link**. This allows you to immediately open or download the submitted document or image.
* **Robust Selection Fields:** For `Selection Element Blocks` (like checkboxes, radio buttons, or dropdowns), the system more reliably identifies and displays the selected options during the quick view process, even if the form item's internal value and displayed caption differ.

---

### 🗺️ How to Access the New Dashboard

The new dashboard is accessible from two primary locations in the Optimizely CMS user interface:

#### 1. Global Navigation Menu
The most direct way to access the dashboard:
* Click the **Global Menu** (the 'hamburger' icon) in the upper-left corner of the CMS.
* Navigate to the new menu item labeled **"Form Submissions"**.

#### 2. From the Form Container Block
For context-specific access to a specific form's data:
* Navigate to a page containing your **Form Container Block** in the CMS Edit view.
* Select the **Form Container Block**.
* In the asset/properties panel, select the custom view named **"Advanced Form Submissions"**. This will open the dashboard pre-filtered to show only submissions for that form.

---

### ⚙️ For Technical Users & Developers

This add-on is implemented using standard Optimizely CMS 12 (.NET Core) extension points:
* **Admin Tooling:** Uses `IMenuProvider` to register the new dashboard link (`CustomAdminMenuProvider.cs`).
* **View Injection:** Utilizes `ViewConfiguration<FormContainerBlock>` (`AdvancedFormSubmissionsViewConfiguration.cs`) to inject the custom submissions view directly into the content editor experience for form blocks.
* **Data Handling:** Overrides the default Optimizely Forms data hydration logic by registering custom `IFormPredefinedValueHandler` implementations (`*PredefinedValueHandler.cs`) and a custom resolver (`FormPredefinedValueHandlerResolver.cs`). This ensures specialized display logic (like the clickable file links) is correctly applied in the Quick View modal.