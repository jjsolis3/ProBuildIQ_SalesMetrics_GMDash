/**

 * Template Field Selector

 * Handles checkbox-based field selection and auto-generates MergeSpec JSON

 */



var TemplateFieldSelector = (function () {

    'use strict';



    // Field definitions with data sources

    const fieldDefinitions = {

        // Property fields

        'PropertyName':    { source: 'Property', type: 'text', displayName: 'Property Name' },

        'PropertyAddress': { source: 'Property', type: 'text', displayName: 'Property Address' },

        'PropertyPhone':   { source: 'Property', type: 'text', displayName: 'Property Phone' },

        'PropertyCity':    { source: 'Property', type: 'text', displayName: 'Property City' },

        'PropertyState':   { source: 'Property', type: 'text', displayName: 'Property State' },

        'PropertyZip':     { source: 'Property', type: 'text', displayName: 'Property Zip Code' },



        // Customer contact fields

        'CustomerName':    { source: 'Customer', type: 'text', displayName: 'Customer Name' },

        'CustomerPhone':   { source: 'Customer', type: 'text', displayName: 'Customer Phone' },

        'CustomerEmail':   { source: 'Customer', type: 'text', displayName: 'Customer Email' },

        'CustomerCompany': { source: 'Customer', type: 'text', displayName: 'Customer Company' },



        // Work Order fields

        'OrderNumber':     { source: 'Order', type: 'text', displayName: 'Order Number' },

        'InstallationDate':{ source: 'Order', type: 'date', displayName: 'Installation Date' },

        'UnitNumber':      { source: 'Order', type: 'text', displayName: 'Unit Number' },

        'DeliveryDate':    { source: 'Order', type: 'date', displayName: 'Delivery Date' },

        'OrderStartDate':  { source: 'Order', type: 'date', displayName: 'Order Start Date' },

        'OrderEndDate':    { source: 'Order', type: 'date', displayName: 'Order End Date' },

        'OrderSignedDate': { source: 'Order', type: 'date', displayName: 'Order Signed Date' },

        'LeaseStartDate':  { source: 'Order', type: 'date', displayName: 'Lease Start Date' },

        'LeaseEndDate':    { source: 'Order', type: 'date', displayName: 'Lease End Date' },



        // Signature fields

        'PropertyStaffName':      { source: 'Recipient', type: 'text',      displayName: 'Property Staff Name',      role: 'Manager' },

        'PropertyStaffSignature': { source: 'Recipient', type: 'signature', displayName: 'Property Staff Signature', role: 'Manager' },

        'PropertyStaffDate':      { source: 'Recipient', type: 'date',      displayName: 'Property Staff Date',      role: 'Manager' },

        'ResidentName':           { source: 'Recipient', type: 'text',      displayName: 'Resident Name',            role: 'Tenant' },

        'ResidentSignature':      { source: 'Recipient', type: 'signature', displayName: 'Resident Signature',       role: 'Tenant' },

        'ResidentPhone':          { source: 'Recipient', type: 'text',      displayName: 'Resident Phone',           role: 'Tenant' }

    };



    function init() {

        setupDisplayNameAutoFill();

        setupTemplateCardSelection();

        setupFieldCheckboxes();

        setupPDFFilePreview();

        generateMergeSpecJSON(); // Generate initial JSON

    }



    /**

     * Auto-generate template key from display name

     */

    function setupDisplayNameAutoFill() {

        const displayNameInput = document.getElementById('displayName');

        const templateKeyInput = document.getElementById('templateKey');



        if (displayNameInput && templateKeyInput) {

            displayNameInput.addEventListener('input', function () {

                const displayName = this.value;

                // Convert to kebab-case: "My Template" => "my-template"

                const templateKey = displayName

                    .toLowerCase()

                    .replace(/[^a-z0-9]+/g, '-')

                    .replace(/^-+|-+$/g, '');



                templateKeyInput.value = templateKey;

            });

        }

    }



    /**

     * Handle template card selection (visual Razor view selector)

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

     * Handle field checkbox selection and JSON generation

     */

    function setupFieldCheckboxes() {

        // Select all checkboxes in category

        const selectAllCheckboxes = document.querySelectorAll('.select-all-category');

        selectAllCheckboxes.forEach(checkbox => {

            checkbox.addEventListener('change', function () {

                const category = this.getAttribute('data-category');

                const categoryCheckboxes = document.querySelectorAll(`.field-checkbox[data-category="${category}"]`);



                categoryCheckboxes.forEach(cb => {

                    cb.checked = this.checked;

                });



                generateMergeSpecJSON();

            });

        });



        // Individual field checkboxes

        const fieldCheckboxes = document.querySelectorAll('.field-checkbox');

        fieldCheckboxes.forEach(checkbox => {

            checkbox.addEventListener('change', function () {

                updateCategoryCheckbox(this.getAttribute('data-category'));

                generateMergeSpecJSON();

            });

        });



        // Initialize category checkboxes

        ['property', 'customer', 'order', 'signature'].forEach(category => {

            updateCategoryCheckbox(category);

        });

    }



    /**

     * Update category "select all" checkbox based on individual selections

     */

    function updateCategoryCheckbox(category) {

        const categoryCheckbox = document.querySelector(`.select-all-category[data-category="${category}"]`);

        const categoryFieldCheckboxes = document.querySelectorAll(`.field-checkbox[data-category="${category}"]`);



        if (!categoryCheckbox || categoryFieldCheckboxes.length === 0) return;



        const allChecked = Array.from(categoryFieldCheckboxes).every(cb => cb.checked);

        const someChecked = Array.from(categoryFieldCheckboxes).some(cb => cb.checked);



        categoryCheckbox.checked = allChecked;

        categoryCheckbox.indeterminate = !allChecked && someChecked;

    }



    /**

     * Generate MergeSpec JSON from selected checkboxes

     */

    function generateMergeSpecJSON() {

        const selectedFields = [];

        const fieldCheckboxes = document.querySelectorAll('.field-checkbox:checked');



        fieldCheckboxes.forEach(checkbox => {

            const fieldKey = checkbox.value;

            const definition = fieldDefinitions[fieldKey];



            if (definition) {

                selectedFields.push({

                    fieldKey: fieldKey,

                    displayName: definition.displayName,

                    source: definition.source,

                    type: definition.type,

                    role: definition.role || null

                });

            }

        });



        // Read existing MergeSpec to preserve placements

        const hiddenInput = document.getElementById('mergeSpecJson');

        let existingSpec = null;

        if (hiddenInput && hiddenInput.value) {

            try {

                existingSpec = JSON.parse(hiddenInput.value);

            } catch (e) {
                // Failed to parse existing MergeSpec - will use empty spec
            }

        }



        // Create MergeSpec JSON

        const mergeSpec = {

            fields: selectedFields.map(f => f.fieldKey),

            fieldMapping: {}

        };



        // Add field mappings, preserving placements from existing spec

        selectedFields.forEach(field => {

            mergeSpec.fieldMapping[field.fieldKey] = {

                source: field.source,

                type: field.type

            };

            if (field.role) {

                mergeSpec.fieldMapping[field.fieldKey].role = field.role;

            }



            // Preserve placements if they exist in the current MergeSpec

            if (existingSpec && existingSpec.fieldMapping && existingSpec.fieldMapping[field.fieldKey]) {

                const existingField = existingSpec.fieldMapping[field.fieldKey];

                if (existingField.placements && existingField.placements.length > 0) {

                    mergeSpec.fieldMapping[field.fieldKey].placements = existingField.placements;

                }

            }

        });



        // Update hidden input and preview

        const jsonString = JSON.stringify(mergeSpec, null, 2);

        const jsonPreview = document.getElementById('jsonPreview');



        if (hiddenInput) {

            hiddenInput.value = jsonString;

        }

        if (jsonPreview) {

            jsonPreview.value = jsonString;

        }

    }



    /**

     * Setup PDF file preview

     */

    function setupPDFFilePreview() {

        const pdfFileInput = document.getElementById('pdfFile');

        const pdfPreview = document.getElementById('pdfPreview');

        const pdfFileName = document.getElementById('pdfFileName');



        if (pdfFileInput) {

            pdfFileInput.addEventListener('change', function () {

                if (this.files && this.files[0]) {

                    const file = this.files[0];

                    if (pdfFileName) {

                        pdfFileName.textContent = file.name;

                    }

                    if (pdfPreview) {

                        pdfPreview.style.display = 'block';

                    }

                } else {

                    if (pdfPreview) {

                        pdfPreview.style.display = 'none';

                    }

                }

            });

        }

    }



    // Public API

    return {

        init: init,

        updateMergeSpec: generateMergeSpecJSON

    };

})();