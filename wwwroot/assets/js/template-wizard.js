/**
 * Template Wizard - Phase 2
 * Handles multi-step wizard navigation, validation, PDF preview, and review
 */

var TemplateWizard = (function () {
    'use strict';

    // State
    let currentStep = 1;
    const totalSteps = 4;
    let pdfDoc = null;
    let currentPage = 1;
    let totalPages = 0;
    let pdfPreviewExpanded = true;

    // Field definitions (matching Phase 1)
    const fieldDefinitions = {
        'PropertyName': { displayName: 'Property Name', category: 'Property' },
        'PropertyAddress': { displayName: 'Property Address', category: 'Property' },
        'PropertyPhone': { displayName: 'Property Phone', category: 'Property' },
        'InstallationDate': { displayName: 'Installation Date', category: 'Order' },
        'UnitNumber': { displayName: 'Unit Number', category: 'Order' },
        'DeliveryDate': { displayName: 'Delivery Date', category: 'Order' },
        'PropertyStaffName': { displayName: 'Property Staff Name', category: 'Signature' },
        'PropertyStaffSignature': { displayName: 'Property Staff Signature', category: 'Signature' },
        'PropertyStaffDate': { displayName: 'Property Staff Date', category: 'Signature' },
        'ResidentName': { displayName: 'Resident Name', category: 'Signature' },
        'ResidentSignature': { displayName: 'Resident Signature', category: 'Signature' },
        'ResidentPhone': { displayName: 'Resident Phone', category: 'Signature' }
    };

    /**
     * Initialize wizard
     */
    function init() {
        setupWizardNavigation();
        setupTemplateCardSelection();
        setupPDFPreview();
        updateWizardUI();
    }

    /**
     * Setup wizard navigation buttons
     */
    function setupWizardNavigation() {
        const nextBtn = document.getElementById('nextBtn');
        const prevBtn = document.getElementById('prevBtn');
        const submitBtn = document.getElementById('submitBtn');

        if (nextBtn) {
            nextBtn.addEventListener('click', function () {
                if (validateCurrentStep()) {
                    nextStep();
                }
            });
        }

        if (prevBtn) {
            prevBtn.addEventListener('click', function () {
                previousStep();
            });
        }

        // Allow clicking on completed steps to navigate
        const wizardSteps = document.querySelectorAll('.wizard-step');
        wizardSteps.forEach(step => {
            step.addEventListener('click', function () {
                const stepNumber = parseInt(this.getAttribute('data-step'));
                if (this.classList.contains('completed') || stepNumber < currentStep) {
                    goToStep(stepNumber);
                }
            });
        });
    }

    /**
     * Navigate to next step
     */
    function nextStep() {
        if (currentStep < totalSteps) {
            // Mark current step as completed
            const currentStepEl = document.querySelector(`.wizard-step[data-step="${currentStep}"]`);
            if (currentStepEl) {
                currentStepEl.classList.add('completed');
            }

            currentStep++;

            // If moving to review step, populate it
            if (currentStep === 4) {
                populateReviewPage();
            }

            updateWizardUI();
        }
    }

    /**
     * Navigate to previous step
     */
    function previousStep() {
        if (currentStep > 1) {
            currentStep--;
            updateWizardUI();
        }
    }

    /**
     * Go to specific step
     */
    function goToStep(stepNumber) {
        if (stepNumber >= 1 && stepNumber <= totalSteps) {
            currentStep = stepNumber;

            // If going to review step, populate it
            if (currentStep === 4) {
                populateReviewPage();
            }

            updateWizardUI();
        }
    }

    /**
     * Update wizard UI (steps, buttons, content)
     */
    function updateWizardUI() {
        // Update step indicators
        const wizardSteps = document.querySelectorAll('.wizard-step');
        wizardSteps.forEach(step => {
            const stepNumber = parseInt(step.getAttribute('data-step'));

            if (stepNumber === currentStep) {
                step.classList.add('active');
            } else {
                step.classList.remove('active');
            }

            // Keep completed class if step is before current
            if (stepNumber < currentStep) {
                step.classList.add('completed');
            }
        });

        // Update step content visibility
        const stepContents = document.querySelectorAll('.wizard-step-content');
        stepContents.forEach(content => {
            const stepNumber = parseInt(content.getAttribute('data-step'));

            if (stepNumber === currentStep) {
                content.classList.add('active');
            } else {
                content.classList.remove('active');
            }
        });

        // Update navigation buttons
        const prevBtn = document.getElementById('prevBtn');
        const nextBtn = document.getElementById('nextBtn');
        const submitBtn = document.getElementById('submitBtn');

        if (prevBtn) {
            prevBtn.style.display = currentStep > 1 ? 'block' : 'none';
        }

        if (nextBtn) {
            nextBtn.style.display = currentStep < totalSteps ? 'block' : 'none';
        }

        if (submitBtn) {
            submitBtn.style.display = currentStep === totalSteps ? 'block' : 'none';
        }

        // Hide validation errors when changing steps
        hideValidationErrors();
    }

    /**
     * Validate current step
     */
    function validateCurrentStep() {
        hideValidationErrors();

        switch (currentStep) {
            case 1:
                return validateStep1();
            case 2:
                return validateStep2();
            case 3:
                return validateStep3();
            case 4:
                return true; // Review step always valid
            default:
                return false;
        }
    }

    /**
     * Validate Step 1: Basic Information
     */
    function validateStep1() {
        const displayName = document.getElementById('displayName')?.value.trim();
        const category = document.getElementById('templateCategory')?.value;

        if (!displayName || !category) {
            showValidationError('step1Error');
            return false;
        }

        return true;
    }

    /**
     * Validate Step 2: Template Selection
     */
    function validateStep2() {
        const razorViewPath = document.getElementById('razorViewPath')?.value;

        if (!razorViewPath) {
            showValidationError('step2Error');
            return false;
        }

        return true;
    }

    /**
     * Validate Step 3: Field Selection
     */
    function validateStep3() {
        const checkedFields = document.querySelectorAll('.field-checkbox:checked');

        if (checkedFields.length === 0) {
            showValidationError('step3Error');
            return false;
        }

        return true;
    }

    /**
     * Show validation error
     */
    function showValidationError(errorId) {
        const errorEl = document.getElementById(errorId);
        if (errorEl) {
            errorEl.classList.add('show');
        }
    }

    /**
     * Hide all validation errors
     */
    function hideValidationErrors() {
        const errors = document.querySelectorAll('.validation-error');
        errors.forEach(error => {
            error.classList.remove('show');
        });
    }

    /**
     * Setup template card selection
     */
    function setupTemplateCardSelection() {
        const cards = document.querySelectorAll('.template-card');
        const hiddenInput = document.getElementById('razorViewPath');

        cards.forEach(card => {
            card.addEventListener('click', function () {
                // Remove selected class from all cards
                cards.forEach(c => c.classList.remove('selected'));

                // Add selected class to clicked card
                this.classList.add('selected');

                // Update hidden input
                const viewPath = this.getAttribute('data-view-path');
                if (hiddenInput) {
                    hiddenInput.value = viewPath;
                }
            });
        });
    }

    /**
     * Setup PDF preview functionality
     */
    function setupPDFPreview() {
        const pdfFileInput = document.getElementById('pdfFile');
        const toggleBtn = document.getElementById('togglePreview');

        if (pdfFileInput) {
            pdfFileInput.addEventListener('change', handlePDFUpload);
        }

        if (toggleBtn) {
            toggleBtn.addEventListener('click', togglePDFPreview);
        }

        // Page navigation
        const prevPageBtn = document.getElementById('prevPage');
        const nextPageBtn = document.getElementById('nextPage');

        if (prevPageBtn) {
            prevPageBtn.addEventListener('click', showPreviousPage);
        }

        if (nextPageBtn) {
            nextPageBtn.addEventListener('click', showNextPage);
        }
    }

    /**
     * Handle PDF file upload
     */
    function handlePDFUpload(event) {
        const file = event.target.files[0];

        if (!file) {
            hidePDFPreview();
            return;
        }

        if (file.type !== 'application/pdf') {
            alert('Please upload a valid PDF file');
            event.target.value = '';
            return;
        }

        // Update file info
        const fileName = document.getElementById('pdfFileName');
        const fileSize = document.getElementById('pdfFileSize');

        if (fileName) {
            fileName.textContent = file.name;
        }

        if (fileSize) {
            const sizeMB = (file.size / (1024 * 1024)).toFixed(2);
            fileSize.textContent = `${sizeMB} MB`;
        }

        // Load and render PDF
        const fileReader = new FileReader();
        fileReader.onload = function (e) {
            loadPDF(e.target.result);
        };
        fileReader.readAsArrayBuffer(file);
    }

    /**
     * Load PDF with PDF.js
     */
    function loadPDF(data) {
        const loadingTask = pdfjsLib.getDocument({ data: data });

        loadingTask.promise.then(function (pdf) {
            pdfDoc = pdf;
            totalPages = pdf.numPages;
            currentPage = 1;

            // Update total pages display
            const totalPagesEl = document.getElementById('totalPages');
            if (totalPagesEl) {
                totalPagesEl.textContent = totalPages;
            }

            // Render first page
            renderPage(currentPage);

            // Show preview container
            showPDFPreview();

            // Show field placement button (Phase 3)
            if (typeof PDFFieldConfigurator !== 'undefined') {
                PDFFieldConfigurator.showEnableButton();
            }

        }).catch(function (error) {
            console.error('Error loading PDF:', error);
            alert('Error loading PDF file. Please try again.');
        });
    }

    /**
     * Render PDF page
     */
    function renderPage(pageNum) {
        if (!pdfDoc) return;

        pdfDoc.getPage(pageNum).then(function (page) {
            const canvas = document.getElementById('pdfCanvas');
            const context = canvas.getContext('2d');

            // Calculate scale to fit container (max width 800px)
            const viewport = page.getViewport({ scale: 1.0 });
            const maxWidth = 800;
            const scale = maxWidth / viewport.width;
            const scaledViewport = page.getViewport({ scale: scale });

            // Set canvas dimensions
            canvas.height = scaledViewport.height;
            canvas.width = scaledViewport.width;

            // Render page
            const renderContext = {
                canvasContext: context,
                viewport: scaledViewport
            };

            page.render(renderContext);

            // Update current page display
            const currentPageEl = document.getElementById('currentPage');
            if (currentPageEl) {
                currentPageEl.textContent = pageNum;
            }

            // Update navigation buttons
            updatePageNavigation();

            // Notify field configurator of page change (Phase 3)
            if (typeof PDFFieldConfigurator !== 'undefined') {
                PDFFieldConfigurator.onPageChange();
            }
        });
    }

    /**
     * Show previous page
     */
    function showPreviousPage() {
        if (currentPage > 1) {
            currentPage--;
            renderPage(currentPage);
        }
    }

    /**
     * Show next page
     */
    function showNextPage() {
        if (currentPage < totalPages) {
            currentPage++;
            renderPage(currentPage);
        }
    }

    /**
     * Update page navigation buttons state
     */
    function updatePageNavigation() {
        const prevBtn = document.getElementById('prevPage');
        const nextBtn = document.getElementById('nextPage');

        if (prevBtn) {
            prevBtn.disabled = currentPage === 1;
        }

        if (nextBtn) {
            nextBtn.disabled = currentPage === totalPages;
        }
    }

    /**
     * Show PDF preview container
     */
    function showPDFPreview() {
        const container = document.getElementById('pdfPreviewContainer');
        if (container) {
            container.style.display = 'block';
            pdfPreviewExpanded = true;
            updateToggleButton();
        }
    }

    /**
     * Hide PDF preview container
     */
    function hidePDFPreview() {
        const container = document.getElementById('pdfPreviewContainer');
        if (container) {
            container.style.display = 'none';
        }

        // Hide field placement button (Phase 3)
        if (typeof PDFFieldConfigurator !== 'undefined') {
            PDFFieldConfigurator.hideEnableButton();
        }

        pdfDoc = null;
        currentPage = 1;
        totalPages = 0;
    }

    /**
     * Toggle PDF preview expanded/collapsed
     */
    function togglePDFPreview() {
        const content = document.getElementById('pdfPreviewContent');
        const toggleBtn = document.getElementById('togglePreview');

        if (pdfPreviewExpanded) {
            // Collapse
            if (content) content.style.display = 'none';
            pdfPreviewExpanded = false;
        } else {
            // Expand
            if (content) content.style.display = 'block';
            pdfPreviewExpanded = true;
        }

        updateToggleButton();
    }

    /**
     * Update toggle button text/icon
     */
    function updateToggleButton() {
        const toggleBtn = document.getElementById('togglePreview');
        if (!toggleBtn) return;

        if (pdfPreviewExpanded) {
            toggleBtn.innerHTML = '<i class="bi bi-arrows-angle-contract"></i> Collapse';
        } else {
            toggleBtn.innerHTML = '<i class="bi bi-arrows-angle-expand"></i> Expand';
        }
    }

    /**
     * Populate review page with all selections
     */
    function populateReviewPage() {
        // Basic Information
        const displayName = document.getElementById('displayName')?.value || '';
        const templateKey = document.getElementById('templateKey')?.value || '';
        const category = document.getElementById('templateCategory')?.value || '';
        const isActive = document.querySelector('[name="IsActive"]')?.value === 'true';

        document.getElementById('reviewDisplayName').textContent = displayName;
        document.getElementById('reviewTemplateKey').textContent = templateKey;
        document.getElementById('reviewCategory').textContent = category;
        document.getElementById('reviewStatus').textContent = isActive ? 'Active' : 'Inactive';

        // Document Template
        const razorView = document.getElementById('razorViewPath')?.value || '';
        document.getElementById('reviewRazorView').textContent = razorView;

        // Selected Fields
        const selectedFields = document.querySelectorAll('.field-checkbox:checked');
        const fieldsContainer = document.getElementById('reviewFields');

        if (fieldsContainer) {
            fieldsContainer.innerHTML = '';

            if (selectedFields.length === 0) {
                fieldsContainer.innerHTML = '<p class="text-muted">No fields selected</p>';
            } else {
                selectedFields.forEach(checkbox => {
                    const fieldKey = checkbox.value;
                    const fieldDef = fieldDefinitions[fieldKey];

                    if (fieldDef) {
                        const badge = document.createElement('span');
                        badge.className = 'field-badge';
                        badge.textContent = fieldDef.displayName;
                        fieldsContainer.appendChild(badge);
                    }
                });
            }
        }

        // PDF File Info
        const pdfFileInput = document.getElementById('pdfFile');
        const pdfSection = document.getElementById('reviewPdfSection');

        if (pdfFileInput?.files && pdfFileInput.files.length > 0) {
            const file = pdfFileInput.files[0];
            document.getElementById('reviewPdfFileName').textContent = file.name;

            const sizeMB = (file.size / (1024 * 1024)).toFixed(2);
            document.getElementById('reviewPdfFileSize').textContent = `${sizeMB} MB`;

            if (pdfSection) {
                pdfSection.style.display = 'block';
            }
        } else {
            if (pdfSection) {
                pdfSection.style.display = 'none';
            }
        }
    }

    // Public API
    return {
        init: init,
        nextStep: nextStep,
        previousStep: previousStep,
        goToStep: goToStep
    };

})();
﻿/**

 * Template Wizard - Multi-step form navigation and PDF preview

 */



var TemplateWizard = (function () {

    'use strict';



    let currentStep = 1;

    let totalSteps = 4;

    let pdfDoc = null;

    let pageNum = 1;

    let pageRendering = false;

    let pageNumPending = null;



    function init() {

        showStep(1);

        setupPdfPreview();

    }



    /**

     * Change wizard step

     */

    window.changeStep = function (direction) {

        const nextStep = currentStep + direction;



        // Validate current step before proceeding

        if (direction > 0 && !validateStep(currentStep)) {

            return;

        }



        if (nextStep >= 1 && nextStep <= totalSteps) {

            currentStep = nextStep;

            showStep(currentStep);



            // Update review page if moving to step 4

            if (currentStep === 4) {

                updateReviewPage();

            }

        }

    };



    /**

     * Show specific step

     */

    function showStep(step) {

        // Hide all steps

        document.querySelectorAll('.wizard-content').forEach(content => {

            content.style.display = 'none';

        });



        // Show current step

        const currentContent = document.querySelector(`.wizard-content[data-step="${step}"]`);

        if (currentContent) {

            currentContent.style.display = 'block';

        }



        // Update progress indicator

        document.querySelectorAll('.wizard-step').forEach((stepEl, index) => {

            stepEl.classList.remove('active', 'completed');

            if (index + 1 === step) {

                stepEl.classList.add('active');

            } else if (index + 1 < step) {

                stepEl.classList.add('completed');

            }

        });



        // Update navigation buttons

        const prevBtn = document.getElementById('prevBtn');

        const nextBtn = document.getElementById('nextBtn');

        const submitBtn = document.getElementById('submitBtn');



        if (prevBtn) prevBtn.style.display = step === 1 ? 'none' : 'inline-block';

        if (nextBtn) nextBtn.style.display = step === totalSteps ? 'none' : 'inline-block';

        if (submitBtn) submitBtn.style.display = step === totalSteps ? 'inline-block' : 'none';



        // Update button text

        if (nextBtn) {

            nextBtn.innerHTML = step === totalSteps - 1

                ? 'Review <i class="bi bi-arrow-right"></i>'

                : 'Next <i class="bi bi-arrow-right"></i>';

        }

    }



    /**

     * Validate current step

     */

    function validateStep(step) {

        switch (step) {

            case 1:

                const displayName = document.getElementById('displayName');

                const category = document.getElementById('templateCategory');



                if (!displayName || !displayName.value.trim()) {

                    alert('Please enter a display name');

                    if (displayName) displayName.focus();

                    return false;

                }



                if (!category || !category.value) {

                    alert('Please select a category');

                    if (category) category.focus();

                    return false;

                }

                return true;



            case 2:

                const razorPath = document.getElementById('razorViewPath');

                if (!razorPath || !razorPath.value) {

                    alert('Please select a document template');

                    return false;

                }

                return true;



            case 3:

                const checkedFields = document.querySelectorAll('.field-checkbox:checked');

                if (checkedFields.length === 0) {

                    alert('Please select at least one field');

                    return false;

                }

                return true;



            default:

                return true;

        }

    }



    /**

     * Update review page with current selections

     */

    function updateReviewPage() {

        // Basic Information

        const displayName = document.getElementById('displayName');

        const templateKey = document.getElementById('templateKey');

        const category = document.getElementById('templateCategory');

        const isActive = document.querySelector('[name="IsActive"]');



        if (displayName) {

            document.getElementById('review_displayName').textContent = displayName.value;

        }

        if (templateKey) {

            document.getElementById('review_templateKey').textContent = templateKey.value;

        }

        if (category) {

            document.getElementById('review_category').textContent = category.options[category.selectedIndex].text;

        }

        if (isActive) {

            document.getElementById('review_active').textContent = isActive.value === 'true' ? 'Yes' : 'No';

        }



        // Razor View Path

        const razorPath = document.getElementById('razorViewPath');

        if (razorPath) {

            document.getElementById('review_razorPath').textContent = razorPath.value || 'Not selected';

        }



        // Selected Fields

        const checkedFields = document.querySelectorAll('.field-checkbox:checked');

        const fieldsContainer = document.getElementById('review_fields');

        if (fieldsContainer) {

            if (checkedFields.length > 0) {

                const fieldsList = Array.from(checkedFields).map(cb => {

                    const label = document.querySelector(`label[for="${cb.id}"]`);

                    return label ? label.textContent : cb.value;

                });

                fieldsContainer.innerHTML = '<div class="d-flex flex-wrap gap-2">' +

                    fieldsList.map(f => `<span class="badge bg-primary">${f}</span>`).join('') +

                    '</div>';

            } else {

                fieldsContainer.innerHTML = '<p class="text-muted mb-0">No fields selected</p>';

            }

        }



        // PDF File

        const pdfFile = document.getElementById('pdfFile');

        const reviewPdfFile = document.getElementById('review_pdfFile');

        if (reviewPdfFile) {

            if (pdfFile && pdfFile.files && pdfFile.files[0]) {

                const file = pdfFile.files[0];

                const fileSize = (file.size / 1024).toFixed(2);

                reviewPdfFile.innerHTML = `<strong>${file.name}</strong> <span class="badge bg-secondary">${fileSize} KB</span>`;

            } else {

                reviewPdfFile.textContent = 'No PDF uploaded';

            }

        }

    }



    /**

     * Setup PDF preview functionality

     */

    function setupPdfPreview() {

        const pdfFileInput = document.getElementById('pdfFile');



        if (pdfFileInput) {

            pdfFileInput.addEventListener('change', function () {

                if (this.files && this.files[0]) {

                    const file = this.files[0];

                    handlePdfFile(file);

                }

            });

        }

    }



    /**

     * Handle PDF file selection

     */

    function handlePdfFile(file) {

        const pdfPreviewContainer = document.getElementById('pdfPreviewContainer');

        const pdfFileName = document.getElementById('pdfFileName');

        const pdfFileSize = document.getElementById('pdfFileSize');



        if (pdfFileName) {

            pdfFileName.textContent = file.name;

        }



        if (pdfFileSize) {

            const fileSize = (file.size / 1024).toFixed(2);

            pdfFileSize.textContent = `${fileSize} KB`;

        }



        if (pdfPreviewContainer) {

            pdfPreviewContainer.style.display = 'block';

        }



        // Load PDF for preview

        const fileReader = new FileReader();

        fileReader.onload = function () {

            const typedarray = new Uint8Array(this.result);

            loadPdf(typedarray);

        };

        fileReader.readAsArrayBuffer(file);

    }



    /**

     * Load PDF using PDF.js

     */

    function loadPdf(data) {

        pdfjsLib.getDocument(data).promise.then(function (pdf) {

            pdfDoc = pdf;

            document.getElementById('totalPages').textContent = pdf.numPages;

            pageNum = 1;

        }).catch(function (error) {

            console.error('Error loading PDF:', error);

            alert('Error loading PDF file. Please ensure it is a valid PDF.');

        });

    }



    /**

     * Show PDF preview

     */

    window.showPdfPreview = function () {

        const pdfPreviewCanvas = document.getElementById('pdfPreviewCanvas');

        if (pdfPreviewCanvas) {

            pdfPreviewCanvas.style.display = 'block';

        }



        if (pdfDoc) {

            renderPage(pageNum);

        }

    };



    /**

     * Close PDF preview

     */

    window.closePdfPreview = function () {

        const pdfPreviewCanvas = document.getElementById('pdfPreviewCanvas');

        if (pdfPreviewCanvas) {

            pdfPreviewCanvas.style.display = 'none';

        }

    };



    /**

     * Change PDF page

     */

    window.changePage = function (direction) {

        if (!pdfDoc) return;



        pageNum += direction;

        if (pageNum < 1) pageNum = 1;

        if (pageNum > pdfDoc.numPages) pageNum = pdfDoc.numPages;



        renderPage(pageNum);

    };



    /**

     * Render specific page

     */

    function renderPage(num) {

        if (!pdfDoc) return;



        pageRendering = true;



        pdfDoc.getPage(num).then(function (page) {

            const canvas = document.getElementById('pdfCanvas');

            const ctx = canvas.getContext('2d');

            const viewport = page.getViewport({ scale: 1.5 });



            canvas.height = viewport.height;

            canvas.width = viewport.width;



            const renderContext = {

                canvasContext: ctx,

                viewport: viewport

            };



            const renderTask = page.render(renderContext);



            renderTask.promise.then(function () {

                pageRendering = false;

                document.getElementById('currentPage').textContent = num;



                if (pageNumPending !== null) {

                    renderPage(pageNumPending);

                    pageNumPending = null;

                }



                // Update button states

                document.getElementById('prevPage').disabled = num === 1;

                document.getElementById('nextPage').disabled = num === pdfDoc.numPages;

            });

        });

    }



    // Public API

    return {

        init: init

    };

})();
