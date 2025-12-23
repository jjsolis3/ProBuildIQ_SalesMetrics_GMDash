/**

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