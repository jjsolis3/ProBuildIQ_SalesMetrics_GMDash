document.addEventListener("DOMContentLoaded", function () {
    const taskColumns = Array.from(document.querySelectorAll(".tasks"));

    const drake = dragula(taskColumns, {
        direction: 'vertical',
        moves: function (el, container, handle) {
            return el.classList.contains("tasks-box");
        },
        accepts: function (el, target, source, sibling) {
            return target.classList.contains("tasks"); // only drop into task columns
        },
        invalid: function (el, handle) {
            return !el.classList.contains("tasks-box"); // prevent dragging non-cards
        }
    });

    drake.on("drop", function (el, target, source, sibling) {
        const taskId = el.dataset.taskId;
        const newStatus = target.dataset.status;

        if (!taskId || !newStatus) return;

        fetch("/Tasks/UpdateStatus", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                taskId: parseInt(taskId),
                newStatus: newStatus
            })
        })
        .then(res => res.json())
        .then(data => {
            if (!data.success) {
                Swal.fire("Error", "Could not update task status.", "error");
            } else {
                updateTaskCounts();
            }
        });
    });

    drake.on("over", (el, container) => container.classList.add("ex-over"));
    drake.on("out", (el, container) => container.classList.remove("ex-over"));
    drake.on("dragend", () => {
        document.querySelectorAll(".ex-over").forEach(el => el.classList.remove("ex-over"));
    });

    function updateTaskCounts() {
        document.querySelectorAll(".tasks-list").forEach(list => {
            const badge = list.querySelector(".totaltask-badge");
            const count = list.querySelectorAll(".tasks-box").length;
            if (badge) badge.textContent = count;
        });
    }

    autoScroll(taskColumns, {
        margin: 20,
        maxSpeed: 10,
        scrollWhenOutside: true,
        autoScroll: function () {
            return this.down && drake.dragging;
        }
    });

    updateTaskCounts();
});
