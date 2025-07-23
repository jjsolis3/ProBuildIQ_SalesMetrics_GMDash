function initDateRangePicker(startDate, endDate) {
    if (!$('#daterange').length) {
        console.warn("Date range input #daterange not found. Skipping init.");
        return;
    }

    function parseDateOrDefault(value, fallback) {
        return moment(value, 'YYYY-MM-DD', true).isValid() ? moment(value) : fallback;
    }

    let start = parseDateOrDefault(startDate, moment().subtract(1, 'months').startOf('month'));
    let end = parseDateOrDefault(endDate, moment());

    function updateDateInputs(start, end) {
        $('#daterange').val(start.format('MMM D, YYYY') + ' - ' + end.format('MMM D, YYYY'));
        $('#startDate').val(start.format('YYYY-MM-DD'));
        $('#endDate').val(end.format('YYYY-MM-DD'));
    }

    $('#daterange').daterangepicker({
        startDate: start,
        endDate: end,
        locale: {
            format: 'MMM D, YYYY'
        },
        ranges: {
            'Today': [moment(), moment()],
            'Yesterday': [moment().subtract(1, 'days'), moment().subtract(1, 'days')],
            'Last 7 Days': [moment().subtract(6, 'days'), moment()],
            'Last 30 Days': [moment().subtract(29, 'days'), moment()],
            'This Month': [moment().startOf('month'), moment().endOf('month')],
            'Last Month': [moment().subtract(1, 'month').startOf('month'), moment().subtract(1, 'month').endOf('month')],
            'This Year (YTD)': [moment().startOf('year'), moment()]
        }
    }, updateDateInputs);

    updateDateInputs(start, end);
}


//function initDateRangePicker(startDate, endDate) {
//    function parseDateOrDefault(value, fallback) {
//        return moment(value, 'YYYY-MM-DD', true).isValid() ? moment(value) : fallback;
//    }

//    let start = parseDateOrDefault(startDate, moment().subtract(1, 'months').startOf('month'));
//    let end = parseDateOrDefault(endDate, moment());

//    function updateDateInputs(start, end) {
//        $('#daterange').val(start.format('MMM D, YYYY') + ' - ' + end.format('MMM D, YYYY'));
//        $('#startDate').val(start.format('YYYY-MM-DD'));
//        $('#endDate').val(end.format('YYYY-MM-DD'));
//    }

//    $('#daterange').daterangepicker({
//        startDate: start,
//        endDate: end,
//        locale: {
//            format: 'MMM D, YYYY'
//        },
//        ranges: {
//            'Today': [moment(), moment()],
//            'Yesterday': [moment().subtract(1, 'days'), moment().subtract(1, 'days')],
//            'Last 7 Days': [moment().subtract(6, 'days'), moment()],
//            'Last 30 Days': [moment().subtract(29, 'days'), moment()],
//            'This Month': [moment().startOf('month'), moment().endOf('month')],
//            'Last Month': [moment().subtract(1, 'month').startOf('month'), moment().subtract(1, 'month').endOf('month')],
//            'This Year (YTD)': [moment().startOf('year'), moment()]
//        }
//    }, updateDateInputs);

//    updateDateInputs(start, end);
//}
