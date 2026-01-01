/**
 * PDF Field Configurator - Phase 3
 * Visual drag-and-drop field positioning on PDF templates
 */

var PDFFieldConfigurator = (function () {
    'use strict';

    // State
    let isPlacementMode = false;
    let selectedFieldForPlacement = null;
    let selectedPlacedField = null;
    let placedFields = []; // Array of { fieldKey, displayName, page, x, y, width, height }
    let pdfScale = 1.0;
    let canvasOffset = { x: 0, y: 0 };
    let isDragging = false;
    let dragStartPos = { x: 0, y: 0 };
    let dragFieldIndex = -1;

    // Field definitions (matching Phase 1 & 2)
    const fieldDefinitions = {
        'PropertyName': { displayName: 'Property Name', category: 'Property', type: 'text' },
        'PropertyAddress': { displayName: 'Property Address', category: 'Property', type: 'text' },
        'PropertyPhone': { displayName: 'Property Phone', category: 'Property', type: 'text' },
        'InstallationDate': { displayName: 'Installation Date', category: 'Order', type: 'date' },
        'UnitNumber': { displayName: 'Unit Number', category: 'Order', type: 'text' },
        'DeliveryDate': { displayName: 'Delivery Date', category: 'Order', type: 'date' },
        'PropertyStaffName': { displayName: 'Property Staff Name', category: 'Signature', type: 'text' },
        'PropertyStaffSignature': { displayName: 'Property Staff Signature', category: 'Signature', type: 'signature' },
        'PropertyStaffDate': { displayName: 'Property Staff Date', category: 'Signature', type: 'date' },
        'ResidentName': { displayName: 'Resident Name', category: 'Signature', type: 'text' },
        'ResidentSignature': { displayName: 'Resident Signature', category: 'Signature', type: 'signature' },
        'ResidentPhone': { displayName: 'Resident Phone', category: 'Signature', type: 'text' }
    };

    /**
     * Initialize field configurator
     */
    function init() {
        setupButtons();
        setupFieldPropertyInputs();

        // Listen for field checkbox changes to update palette
        const fieldCheckboxes = document.querySelectorAll('.field-checkbox');
        fieldCheckboxes.forEach(checkbox => {
            checkbox.addEventListener('change', updateFieldPalette);
        });
    }

    /**
     * Setup button event listeners
     */
    function setupButtons() {
        const enableBtn = document.getElementById('enableFieldPlacement');
        const disableBtn = document.getElementById('disableFieldPlacement');
        const clearAllBtn = document.getElementById('clearAllFields');
        const deleteBtn = document.getElementById('deleteFieldBtn');

        if (enableBtn) {
            enableBtn.addEventListener('click', enablePlacementMode);
        }

        if (disableBtn) {
            disableBtn.addEventListener('click', disablePlacementMode);
        }

        if (clearAllBtn) {
            clearAllBtn.addEventListener('click', clearAllFields);
        }

        if (deleteBtn) {
            deleteBtn.addEventListener('click', deleteSelectedField);
        }
    }

    /**
     * Setup field property input listeners
     */
    function setupFieldPropertyInputs() {
        const xInput = document.getElementById('fieldPropX');
        const yInput = document.getElementById('fieldPropY');
        const widthInput = document.getElementById('fieldPropWidth');
        const heightInput = document.getElementById('fieldPropHeight');

        [xInput, yInput, widthInput, heightInput].forEach(input => {
            if (input) {
                input.addEventListener('change', updateSelectedFieldProperties);
            }
        });
    }

    /**
     * Enable field placement mode
     */
    function enablePlacementMode() {
        isPlacementMode = true;

        // Show configurator panel
        const panel = document.getElementById('fieldConfiguratorPanel');
        if (panel) {
            panel.style.display = 'block';
        }

        // Hide enable button
        const enableBtn = document.getElementById('enableFieldPlacement');
        if (enableBtn) {
            enableBtn.style.display = 'none';
        }

        // Update field palette
        updateFieldPalette();

        // Setup PDF click handler
        setupPDFClickHandler();

        // Show field overlay
        const overlay = document.getElementById('fieldOverlay');
        if (overlay) {
            overlay.style.display = 'block';
            overlay.style.pointerEvents = 'none'; // Allow clicks through to canvas, except on field boxes
            updateOverlayDimensions();
        }

        // Render existing fields
        renderFieldsOnPDF();
    }

    /**
     * Disable field placement mode
     */
    function disablePlacementMode() {
        isPlacementMode = false;

        // Hide configurator panel
        const panel = document.getElementById('fieldConfiguratorPanel');
        if (panel) {
            panel.style.display = 'none';
        }

        // Show enable button
        const enableBtn = document.getElementById('enableFieldPlacement');
        if (enableBtn) {
            enableBtn.style.display = 'inline-block';
        }

        // Hide field overlay
        const overlay = document.getElementById('fieldOverlay');
        if (overlay) {
            overlay.style.display = 'none';
        }

        // Save coordinates to MergeSpec JSON
        saveCoordinatesToMergeSpec();

        // Remove PDF click handler
        const canvasWrapper = document.querySelector('.pdf-canvas-wrapper');
        if (canvasWrapper) {
            canvasWrapper.replaceWith(canvasWrapper.cloneNode(true));
        }
    }

    /**
     * Update field palette based on selected checkboxes
     */
    function updateFieldPalette() {
        const palette = document.getElementById('fieldPalette');
        if (!palette) return;

        palette.innerHTML = '';

        const selectedFields = document.querySelectorAll('.field-checkbox:checked');

        if (selectedFields.length === 0) {
            palette.innerHTML = '<p class="text-muted small">Select fields in the checkboxes above first</p>';
            return;
        }

        selectedFields.forEach(checkbox => {
            const fieldKey = checkbox.value;
            const fieldDef = fieldDefinitions[fieldKey];

            if (fieldDef) {
                const item = document.createElement('div');
                item.className = 'field-palette-item';

                // Show count of placements if field is used multiple times
                const placementCount = placedFields.filter(f => f.fieldKey === fieldKey).length;
                if (placementCount > 0) {
                    item.textContent = `${fieldDef.displayName} (${placementCount})`;
                    item.title = `Click to place another instance (${placementCount} already placed)`;
                } else {
                    item.textContent = fieldDef.displayName;
                }

                item.setAttribute('data-field-key', fieldKey);
                item.addEventListener('click', () => selectFieldForPlacement(fieldKey));

                palette.appendChild(item);
            }
        });
    }

    /**
     * Select a field from palette for placement
     */
    function selectFieldForPlacement(fieldKey) {
        selectedFieldForPlacement = fieldKey;

        // Update palette UI
        const paletteItems = document.querySelectorAll('.field-palette-item');
        paletteItems.forEach(item => {
            item.classList.remove('selected');
            if (item.getAttribute('data-field-key') === fieldKey) {
                item.classList.add('selected');
            }
        });
    }

    /**
     * Setup PDF click handler for placing fields
     */
    function setupPDFClickHandler() {
        const canvas = document.getElementById('pdfCanvas');
        const overlay = document.getElementById('fieldOverlay');

        if (!canvas || !overlay) return;

        // Update canvas offset
        updateCanvasOffset();

        // Canvas click for placing fields
        canvas.addEventListener('click', function (e) {
            if (!isPlacementMode || !selectedFieldForPlacement) return;

            const rect = canvas.getBoundingClientRect();
            const x = e.clientX - rect.left;
            const y = e.clientY - rect.top;

            placeFieldAtPosition(selectedFieldForPlacement, x, y);
        });

        // Overlay mouse events for dragging
        overlay.addEventListener('mousedown', handleDragStart);
        overlay.addEventListener('mousemove', handleDragMove);
        overlay.addEventListener('mouseup', handleDragEnd);
        overlay.addEventListener('mouseleave', handleDragEnd);
    }

    /**
     * Place field at specified position
     */
    function placeFieldAtPosition(fieldKey, x, y) {
        const fieldDef = fieldDefinitions[fieldKey];
        if (!fieldDef) return;

        // ALLOW DUPLICATE PLACEMENTS - removed the check that prevented placing the same field multiple times
        // Users can now place "Resident Name" in multiple locations (e.g., top and bottom of form)

        // Get current page number
        const currentPageEl = document.getElementById('currentPage');
        const currentPage = currentPageEl ? parseInt(currentPageEl.textContent) : 1;

        // Convert screen coordinates to PDF coordinates
        const pdfCoords = screenToPDFCoordinates(x, y);

        // Default dimensions based on field type
        const defaultWidth = fieldDef.type === 'signature' ? 200 : 150;
        const defaultHeight = fieldDef.type === 'signature' ? 50 : 20;

        // Generate unique ID for this field instance (allows multiple instances of same field)
        const instanceId = `${fieldKey}_${Date.now()}`;

        // Add field to placed fields
        const field = {
            fieldKey: fieldKey,
            instanceId: instanceId,
            displayName: fieldDef.displayName,
            type: fieldDef.type,
            page: currentPage,
            x: pdfCoords.x,
            y: pdfCoords.y,
            width: defaultWidth,
            height: defaultHeight
        };

        placedFields.push(field);

        // Clear selection
        selectedFieldForPlacement = null;

        // Update UI
        updateFieldPalette();
        updatePlacedFieldsList();
        renderFieldsOnPDF();
    }

    /**
     * Convert screen coordinates to PDF coordinates
     * Screen coords are in pixels relative to canvas display
     * PDF coords are in points (72 points per inch) in the actual PDF
     */
    function screenToPDFCoordinates(screenX, screenY) {
        const canvas = document.getElementById('pdfCanvas');
        if (!canvas) return { x: screenX, y: screenY };

        // Use the PDF scale factor set when rendering the PDF
        // This is set in window.pdfScale by the template wizard
        const scale = window.pdfScale || 1.0;

        // Convert screen pixels to PDF points
        // If PDF is scaled down (e.g., 0.5), we divide to get actual PDF coordinates
        const pdfX = Math.round(screenX / scale);
        const pdfY = Math.round(screenY / scale);

        return { x: pdfX, y: pdfY };
    }

    /**
     * Convert PDF coordinates to screen coordinates
     * PDF coords are in points, screen coords are in pixels
     */
    function pdfToScreenCoordinates(pdfX, pdfY) {
        const canvas = document.getElementById('pdfCanvas');
        if (!canvas) return { x: pdfX, y: pdfY };

        // Use the PDF scale factor set when rendering the PDF
        const scale = window.pdfScale || 1.0;

        // Convert PDF points to screen pixels
        // If PDF is scaled down (e.g., 0.5), we multiply to get screen coordinates
        const screenX = pdfX * scale;
        const screenY = pdfY * scale;

        return { x: screenX, y: screenY };
    }

    /**
     * Update canvas offset for accurate positioning
     */
    function updateCanvasOffset() {
        const canvas = document.getElementById('pdfCanvas');
        if (!canvas) return;

        const rect = canvas.getBoundingClientRect();
        canvasOffset = {
            x: rect.left,
            y: rect.top
        };
    }

    /**
     * Update overlay dimensions to match canvas
     */
    function updateOverlayDimensions() {
        const canvas = document.getElementById('pdfCanvas');
        const overlay = document.getElementById('fieldOverlay');

        if (!canvas || !overlay) return;

        overlay.setAttribute('width', canvas.width);
        overlay.setAttribute('height', canvas.height);
        overlay.style.width = canvas.style.width || canvas.width + 'px';
        overlay.style.height = canvas.style.height || canvas.height + 'px';
    }

    /**
     * Render all placed fields on PDF
     */
    function renderFieldsOnPDF() {
        const overlay = document.getElementById('fieldOverlay');
        const currentPageEl = document.getElementById('currentPage');

        if (!overlay || !currentPageEl) return;

        const currentPage = parseInt(currentPageEl.textContent);

        // Clear overlay
        overlay.innerHTML = '';

        // Update overlay dimensions
        updateOverlayDimensions();

        // Render fields for current page
        placedFields.forEach((field, index) => {
            if (field.page === currentPage) {
                renderFieldBox(field, index);
            }
        });
    }

    /**
     * Render a single field box on the overlay
     */
    function renderFieldBox(field, index) {
        const overlay = document.getElementById('fieldOverlay');
        if (!overlay) return;

        // Convert PDF coordinates to screen coordinates
        const screenCoords = pdfToScreenCoordinates(field.x, field.y);
        const scale = window.pdfScale || 1.0;
        const screenWidth = field.width * scale;
        const screenHeight = field.height * scale;

        // Create SVG group for field
        const group = document.createElementNS('http://www.w3.org/2000/svg', 'g');
        group.setAttribute('data-field-index', index);

        // Create rectangle
        const rect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
        rect.setAttribute('x', screenCoords.x);
        rect.setAttribute('y', screenCoords.y);
        rect.setAttribute('width', screenWidth);
        rect.setAttribute('height', screenHeight);
        rect.setAttribute('class', 'field-box');
        rect.setAttribute('data-field-index', index);

        if (selectedPlacedField === index) {
            rect.classList.add('selected');
        }

        // Create label
        const text = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        text.setAttribute('x', screenCoords.x + 4);
        text.setAttribute('y', screenCoords.y + 12);
        text.setAttribute('class', 'field-label');
        text.textContent = field.displayName;

        group.appendChild(rect);
        group.appendChild(text);

        // Click to select field
        rect.addEventListener('click', function (e) {
            e.stopPropagation();
            selectPlacedField(index);
        });

        overlay.appendChild(group);
    }

    /**
     * Handle drag start
     */
    function handleDragStart(e) {
        if (!isPlacementMode) return;

        const target = e.target;
        if (!target.classList.contains('field-box')) return;

        isDragging = true;
        dragFieldIndex = parseInt(target.getAttribute('data-field-index'));
        dragStartPos = {
            x: e.clientX,
            y: e.clientY
        };

        // Select the field being dragged
        selectPlacedField(dragFieldIndex);

        e.preventDefault();
    }

    /**
     * Handle drag move
     */
    function handleDragMove(e) {
        if (!isDragging || dragFieldIndex < 0) return;

        const deltaX = e.clientX - dragStartPos.x;
        const deltaY = e.clientY - dragStartPos.y;

        // Update field position
        const field = placedFields[dragFieldIndex];
        if (field) {
            const pdfDelta = screenToPDFCoordinates(deltaX, deltaY);
            const originalPdfCoords = screenToPDFCoordinates(0, 0);

            field.x += pdfDelta.x - originalPdfCoords.x;
            field.y += pdfDelta.y - originalPdfCoords.y;

            // Prevent negative coordinates
            field.x = Math.max(0, field.x);
            field.y = Math.max(0, field.y);

            // Update drag start position
            dragStartPos = {
                x: e.clientX,
                y: e.clientY
            };

            // Re-render
            renderFieldsOnPDF();
            updateFieldPropertiesPanel();
        }
    }

    /**
     * Handle drag end
     */
    function handleDragEnd(e) {
        if (isDragging) {
            isDragging = false;
            dragFieldIndex = -1;
        }
    }

    /**
     * Select a placed field
     */
    function selectPlacedField(index) {
        selectedPlacedField = index;

        // Update placed fields list UI
        updatePlacedFieldsList();

        // Update field properties panel
        updateFieldPropertiesPanel();

        // Re-render to show selection
        renderFieldsOnPDF();
    }

    /**
     * Update placed fields list
     */
    function updatePlacedFieldsList() {
        const list = document.getElementById('placedFieldsList');
        if (!list) return;

        list.innerHTML = '';

        if (placedFields.length === 0) {
            list.innerHTML = '<p class="text-muted small">No fields placed yet</p>';
            return;
        }

        placedFields.forEach((field, index) => {
            const item = document.createElement('div');
            item.className = 'placed-field-item';
            if (selectedPlacedField === index) {
                item.classList.add('active');
            }

            item.innerHTML = `
                <div class="fw-bold">${field.displayName}</div>
                <small class="text-muted">Page ${field.page} • (${field.x}, ${field.y})</small>
            `;

            item.addEventListener('click', () => selectPlacedField(index));

            list.appendChild(item);
        });
    }

    /**
     * Update field properties panel
     */
    function updateFieldPropertiesPanel() {
        const panel = document.getElementById('fieldPropertiesPanel');

        if (selectedPlacedField === null || selectedPlacedField < 0) {
            if (panel) panel.style.display = 'none';
            return;
        }

        const field = placedFields[selectedPlacedField];
        if (!field) return;

        if (panel) panel.style.display = 'block';

        // Populate inputs
        const nameInput = document.getElementById('fieldPropName');
        const xInput = document.getElementById('fieldPropX');
        const yInput = document.getElementById('fieldPropY');
        const widthInput = document.getElementById('fieldPropWidth');
        const heightInput = document.getElementById('fieldPropHeight');

        if (nameInput) nameInput.value = field.displayName;
        if (xInput) xInput.value = field.x;
        if (yInput) yInput.value = field.y;
        if (widthInput) widthInput.value = field.width;
        if (heightInput) heightInput.value = field.height;
    }

    /**
     * Update selected field properties from inputs
     */
    function updateSelectedFieldProperties() {
        if (selectedPlacedField === null || selectedPlacedField < 0) return;

        const field = placedFields[selectedPlacedField];
        if (!field) return;

        const xInput = document.getElementById('fieldPropX');
        const yInput = document.getElementById('fieldPropY');
        const widthInput = document.getElementById('fieldPropWidth');
        const heightInput = document.getElementById('fieldPropHeight');

        if (xInput) field.x = parseInt(xInput.value) || field.x;
        if (yInput) field.y = parseInt(yInput.value) || field.y;
        if (widthInput) field.width = parseInt(widthInput.value) || field.width;
        if (heightInput) field.height = parseInt(heightInput.value) || field.height;

        // Re-render
        renderFieldsOnPDF();
        updatePlacedFieldsList();
    }

    /**
     * Delete selected field
     */
    function deleteSelectedField() {
        if (selectedPlacedField === null || selectedPlacedField < 0) return;

        if (!confirm('Delete this field?')) return;

        placedFields.splice(selectedPlacedField, 1);
        selectedPlacedField = null;

        // Update UI
        updateFieldPalette();
        updatePlacedFieldsList();
        updateFieldPropertiesPanel();
        renderFieldsOnPDF();
    }

    /**
     * Clear all placed fields
     */
    function clearAllFields() {
        if (placedFields.length === 0) return;

        if (!confirm('Clear all placed fields?')) return;

        placedFields = [];
        selectedPlacedField = null;

        // Update UI
        updateFieldPalette();
        updatePlacedFieldsList();
        updateFieldPropertiesPanel();
        renderFieldsOnPDF();
    }

    /**
     * Save field coordinates to MergeSpec JSON
     * Supports multiple placements of the same field
     */
    function saveCoordinatesToMergeSpec() {
        const mergeSpecInput = document.getElementById('mergeSpecJson');
        if (!mergeSpecInput) return;

        try {
            const existingSpec = mergeSpecInput.value ? JSON.parse(mergeSpecInput.value) : {};

            // Group placed fields by fieldKey to support multiple placements
            if (existingSpec.fieldMapping) {
                // First, clear all existing placements
                Object.keys(existingSpec.fieldMapping).forEach(fieldKey => {
                    existingSpec.fieldMapping[fieldKey].placements = [];
                });

                // Then add all current placements
                placedFields.forEach(field => {
                    if (existingSpec.fieldMapping[field.fieldKey]) {
                        if (!existingSpec.fieldMapping[field.fieldKey].placements) {
                            existingSpec.fieldMapping[field.fieldKey].placements = [];
                        }

                        existingSpec.fieldMapping[field.fieldKey].placements.push({
                            page: field.page,
                            x: field.x,
                            y: field.y,
                            width: field.width,
                            height: field.height
                        });
                    }
                });
            }

            // Update the hidden input
            mergeSpecInput.value = JSON.stringify(existingSpec, null, 2);

        } catch (error) {
            console.error('Error saving coordinates to MergeSpec:', error);
        }
    }

    /**
     * Show enable button when PDF is loaded
     */
    function showEnableButton() {
        const enableBtn = document.getElementById('enableFieldPlacement');
        if (enableBtn) {
            enableBtn.style.display = 'inline-block';
        }
    }

    /**
     * Hide enable button
     */
    function hideEnableButton() {
        const enableBtn = document.getElementById('enableFieldPlacement');
        if (enableBtn) {
            enableBtn.style.display = 'none';
        }
    }

    /**
     * Re-render fields when page changes
     */
    function onPageChange() {
        if (isPlacementMode) {
            updateOverlayDimensions();
            renderFieldsOnPDF();
        }
    }

    // Public API
    return {
        init: init,
        showEnableButton: showEnableButton,
        hideEnableButton: hideEnableButton,
        onPageChange: onPageChange,
        renderFieldsOnPDF: renderFieldsOnPDF
    };

})();
