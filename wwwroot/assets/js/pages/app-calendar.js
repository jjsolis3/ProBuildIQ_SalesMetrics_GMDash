// app-calendar.js (corrected version)

class CalendarSchedule {
    constructor() {
        this.modal = new bootstrap.Modal(document.getElementById("createTaskModal"), { backdrop: "static" });
        this.calendar = document.getElementById("calendar");
        this.formEvent = document.getElementById("forms-event");
        this.btnNewEvent = document.getElementById("btn-new-event");
        this.btnDeleteEvent = document.getElementById("btn-delete-event");
        this.btnSaveEvent = document.getElementById("btn-save-event");
        this.modalTitle = document.getElementById("modal-title");

        this.calendarObj = null;
        this.selectedEvent = null;
        this.newEventData = null;
    }

    onEventClick(event) {
        const task = event.event;

        // Reset all event colors (optional)
        document.querySelectorAll('.fc-event').forEach(el => {
            el.style.color = "";
            el.style.border = "";
        });

        // Highlight selected event
        if (event.el) {
            event.el.style.color = "#212529"; // Bootstrap dark
            event.el.style.border = "2px solid orange"; // Optional: orange border
        }

        document.getElementById("task-title").textContent = task.title || "-";
        document.getElementById("task-type").textContent = task.extendedProps.type || "-";
        document.getElementById("task-status").textContent = task.extendedProps.status || "-";
        document.getElementById("task-property").textContent = task.extendedProps.property || "-";
        document.getElementById("task-date").textContent = task.start?.toLocaleString() || "-";
        document.getElementById("task-desc").textContent = task.extendedProps.description || "-";
        document.getElementById("task-assignedTo").textContent = task.extendedProps.assignedTo || "-";

        // Load Task Notes
        const notesContainer = document.getElementById("task-notes");
        const addNoteBtn = document.getElementById("add-note-btn");
        const newNoteInput = document.getElementById("new-note-input");

        const taskId = task.id;
        notesContainer.innerHTML = '<div class="text-muted center"><i class="fa fa-spinner fa-spin"></i></div>'; // Loading spinner'
        fetch(`/Tasks/GetNotesForTask?taskId=${taskId}`)
            .then(response => response.json())
            .then(notes => {
                if (notes.length === 0) {
                    notesContainer.innerHTML = '<div class="text-muted center">No notes available.</div>';
                } else {
                    notesContainer.innerHTML = notes.map(n =>
                        `<div class="mb-1 border-bottom pb-1">
                            <div class="fs-6">${n.noteText}</div>
                            <div class="small"><strong>${n.createdBy}</strong> <span class="text-muted">(${new Date(n.createdDate).toLocaleString()})</span></div>
                        </div>`
                    ).join('');
                }
            })
            .catch(error => {
                notesContainer.innerHTML = '<div class="text-danger center">Error loading notes.</div>';
            });

        // Enable Note Posting
        addNoteBtn.onclick = () => {
            const noteText = newNoteInput.value.trim();
            if (!noteText) return;

            fetch(`/Tasks/AddNoteToTask?taskId=${taskId}&noteText=${encodeURIComponent(noteText)}`, { method: 'POST' })
                .then(response => {
                    if (response.ok) {
                        newNoteInput.value = ''; // Clear input
                        addNoteBtn.disabled = true; // Disable button
                        // Re-Fetch notes
                        return fetch(`/Tasks/GetNotesForTask?taskId=${taskId}`);
                    } else {
                        throw new Error('Failed to add note.');
                    }
                })
                .then(response => response.json())
                .then(notes => {
                    notesContainer.innerHTML = notes.map(n =>
                        `<div class="mb-1 border-bottom pb-1">
                            <div class="fs-6">${n.noteText}</div>
                            <div class="small"><strong>${n.createdBy}</strong> <span class="text-muted">(${new Date(n.createdDate).toLocaleString()})</span></div>
                        </div>`
                    ).join('');
                    addNoteBtn.disabled = false; // Re-enable button
                })
                .catch(error => {
                    console.error(error);
                    notesContainer.innerHTML = '<div class="text-danger center">Error loading notes.</div>';
                    addNoteBtn.disabled = false; // Re-enable button
                });
        };

        // Show Edit Button
        const editBtn = document.getElementById("edit-task-btn");
        editBtn.style.display = "inline-block";
        editBtn.onclick = function () {
            // Open Modal to edit the task
            const taskId = task.id
            CalendarSchedule.loadEditModal(taskId);
        }
    }

    onSelect(selectionInfo) {
        const rawDate = selectionInfo.date || selectionInfo.start; // FullCalendar uses `date` or `start`
        // Set dueDate and dueTime inputs
        const date = rawDate.toISOString().split("T")[0];
        const time = rawDate.toLocaleTimeString('en-GB', {
            hour: '2-digit',
            minute: '2-digit'
        });

        // Update inputs only if they exist
        const dueDateInput = document.getElementById('Task_DueDateDate');
        const dueTimeInput = document.getElementById('Task_DueDateTime');
        if (dueDateInput) dueDateInput.value = date;
        if (dueTimeInput) dueTimeInput.value = time;

        // Clear any old task values
        const titleInput = document.getElementById('event-title');
        if (titleInput) titleInput.value = '';

        const categoryInput = document.getElementById('event-category');
        if (categoryInput) categoryInput.value = 'bg-primary';

        // Show modal
        this.modal.show();
    }

    init() {
        console.log("✅ Calendar Element:", this.calendar);

        const initialEvents = window.initialEvents || [];

        // No plugins needed — already bundled in main.min.js
        this.calendarObj = new FullCalendar.Calendar(this.calendar, {
            initialView: "dayGridMonth",
            themeSystem: "bootstrap5",
            headerToolbar: {
                left: "prev,next today",
                center: "title",
                right: "dayGridMonth,timeGridWeek,timeGridDay,listMonth"
            },
            events: function (fetchInfo, successCallback, failureCallback) {
                const hideCompleted = document.getElementById('hideCompletedToggle')?.checked ? true : false;

                fetch(`/Tasks/GetCalendarEvents?hideCompleted=${hideCompleted}`)
                    .then(response => response.json())
                    .then(events => {
                        const selectedUser = document.getElementById('userFilter')?.value;

                        if (selectedUser) {
                            events = events.filter(e => e.extendedProps.assignedTo === selectedUser);
                        }

                        successCallback(events);
                    })
                    .catch(error => {
                        console.error('Error fetching events:', error);
                        failureCallback(error);
                    });
            },
            noEventsContent: function () {
                return `
                    <div class="text-center py-5">
                        <h5 class="text-muted">
                            🚫 No Active Tasks Available
                        </h5>
                    </div>
                `;
            },
            editable: true,
            selectable: true,
            eventClick: (info) => this.onEventClick(info),
            dateClick: (info) => this.onSelect(info),
            eventStartEditable: true, // default
            eventDurationEditable: true, // default

            eventDataTransform: function (eventData) {
                if (eventData.extendedProps.status === "Completed") {
                    eventData.editable = false;
                    eventData.startEditable = false;  // ❗ Disable dragging
                    eventData.durationEditable = false; // ❗ Disable resizing
                }
                return eventData;
            },
            slotDuration: "0:30:00",
            slotMinTime: "06:00:00",
            slotMaxTime: "20:00:00",
            
            eventDrop: async (info) => {
                const task = info.event;
                const newDate = task.start;

                // Optional: Show a loading indicator or confirmation
                console.log("Task dropped:", task.title, newDate);

                // Send the update to the server
                try {
                    const response = await fetch('/Tasks/UpdateDueDate', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                            'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
                        },
                        body: JSON.stringify({
                            taskId: task.id,
                            title: task.title,
                            newDate: newDate.toISOString()
                        })
                    });

                    const result = await response.json();
                    if (!result.success) {
                        alert("Failed to update task time.");
                        info.revert(); // Rollback
                    }
                } catch (error) {
                    alert("Error updating task.");
                    info.revert(); // Rollback
                }
            }
        });

        this.calendarObj.render();

        document.getElementById('userFilter')?.addEventListener('change', () => {
            this.calendarObj.refetchEvents();  // 🔥 reload calendar when filter changes
        });

        this.btnNewEvent?.addEventListener("click", () => {
            this.onSelect({ date: new Date(), allDay: true });
        });

        this.formEvent?.addEventListener("submit", (e) => {
            e.preventDefault();
            const isValid = this.formEvent.checkValidity();

            if (isValid) {
                const title = document.getElementById("event-title").value;
                const className = document.getElementById("event-category").value;

                if (this.selectedEvent) {
                    this.selectedEvent.setProp("taskId", [taskId])
                    this.selectedEvent.setProp("title", [title]);
                    this.selectedEvent.setProp("classNames", [className]);
                    this.assignedTo.setProp("assignedTo", [assignedTo])
                } else if (this.newEventData) {
                    this.calendarObj.addEvent({
                        title: title,
                        start: this.newEventData.date,
                        End: this.newEventData.date,
                        allDay: this.newEventData.allDay,
                        className: className
                    });
                }
                this.modal.hide();
            } else {
                e.stopPropagation();
                this.formEvent.classList.add("was-validated");
            }
        });

        this.btnDeleteEvent?.addEventListener("click", () => {
            if (this.selectedEvent) {
                this.selectedEvent.remove();
                this.selectedEvent = null;
                this.modal.hide();
            }
        });

        document.getElementById("toggleView")?.addEventListener("click", () => {
            const currentView = this.calendarObj.view.type;
            const nextView = currentView === "dayGridMonth" ? "timeGridWeek" : "dayGridMonth";
            this.calendarObj.changeView(nextView);
        });
    }

    static async loadEditModal(taskId) {
        const url = `/Tasks/EditModalPartial/${taskId}`;

        try {
            const response = await fetch(url);
            const htmlContent = await response.text();

            // Create a wrapper modal element
            const modalWrapper = document.createElement('div');
            modalWrapper.innerHTML = `
                <div class="modal fade" id="dynamicEditModal" tabindex="-1" aria-hidden="true">
                    <div class="modal-dialog modal-dialog-centered">
                        <div class="modal-content">
                            ${htmlContent}
                        </div>
                    </div>
                </div>
            `;

            document.body.appendChild(modalWrapper);

            const modalElement = document.getElementById('dynamicEditModal');
            const bootstrapModal = new bootstrap.Modal(modalElement);
            bootstrapModal.show();

            // Remove modal from DOM after it is closed
            modalElement.addEventListener('hidden.bs.modal', () => {
                modalWrapper.remove();
            });
        } catch (error) {
            console.error('Error loading edit modal:', error);
        }
    }
}

document.addEventListener("DOMContentLoaded", () => {
    const calendar = new CalendarSchedule();
    calendar.init();

    // Expose globally
    window.salesMetricsCalendar = calendar.calendarObj;
});
