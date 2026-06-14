// GlycemicTracker main JavaScript file

let glucoseChart = null;

document.addEventListener("DOMContentLoaded", function () {
    // 1. Initialize Autocomplete for Food Search
    initializeFoodAutocomplete();

    // 2. Initialize Glucose Chart
    initializeGlucoseChart("Today");

    // 3. Initialize Quick Time Offset buttons
    initializeTimeOffsets();

    // 4. Initialize Bootstrap Tooltips
    const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    tooltipTriggerList.map(function (tooltipTriggerEl) {
        return new bootstrap.Tooltip(tooltipTriggerEl);
    });
});

// Setup food autocomplete using jQuery UI
function initializeFoodAutocomplete() {
    const $searchBox = $("#foodSearchInput");
    if ($searchBox.length === 0) return;

    $searchBox.autocomplete({
        source: function (request, response) {
            $.ajax({
                url: "/Home/SearchFoods",
                type: "GET",
                data: { term: request.term },
                success: function (data) {
                    response(data);
                }
            });
        },
        minLength: 1,
        select: function (event, ui) {
            // Set food values on selection
            $("#selectedFoodId").val(ui.item.id);
            $("#foodSelectedAlert").removeClass("d-none");
            $("#selectedFoodName").text(ui.item.value);
            
            // Set stats details
            $("#selectedFoodGI").text(ui.item.gi);
            $("#selectedFoodCarbs").text(ui.item.carbs + "g");
            $("#selectedFoodSugar").text(ui.item.sugar + "g");
            $("#selectedFoodProtein").text(ui.item.protein + "g");
            $("#selectedFoodFat").text(ui.item.fat + "g");
            $("#selectedFoodFiber").text(ui.item.fiber + "g");
            
            // Set portion helper text
            updatePortionHelper(ui.item.value);
            
            // Set GI badge class
            const $giBadge = $("#selectedFoodGIBadge");
            $giBadge.removeClass("gi-low gi-medium gi-high");
            if (ui.item.gi <= 55) {
                $giBadge.addClass("gi-low").text("Low GI");
            } else if (ui.item.gi <= 70) {
                $giBadge.addClass("gi-medium").text("Med GI");
            } else {
                $giBadge.addClass("gi-high").text("High GI");
            }

            // Enable log button
            $("#btnSubmitLog").prop("disabled", false);

            // Trigger preview calculation
            calculateLogPreview(ui.item.gi, ui.item.carbs);
        }
    });

    // Re-calculate preview when portion amount changes
    $("#portionGramsInput").on("input", function () {
        const item = $searchBox.data("ui-autocomplete").selectedItem;
        if (item) {
            calculateLogPreview(item.gi, item.carbs);
        }
    });
}

// Simple preview of GL and carbs for the entered portion
function calculateLogPreview(gi, carbsPer100g) {
    const portionGrams = parseFloat($("#portionGramsInput").val()) || 0;
    const calculatedCarbs = (carbsPer100g * portionGrams) / 100.0;
    const glycemicLoad = (calculatedCarbs * gi) / 100.0;

    $("#previewCarbs").text(calculatedCarbs.toFixed(1) + "g");
    $("#previewGL").text(glycemicLoad.toFixed(1));

    const $glBadge = $("#previewGLBadge");
    $glBadge.removeClass("gi-low gi-medium gi-high");
    if (glycemicLoad <= 10) {
        $glBadge.addClass("gi-low").text("Low GL");
    } else if (glycemicLoad <= 19) {
        $glBadge.addClass("gi-medium").text("Medium GL");
    } else {
        $glBadge.addClass("gi-high").text("High GL");
    }
}

// Quick log time offsets
function initializeTimeOffsets() {
    $(".time-preset-btn").on("click", function () {
        $(".time-preset-btn").removeClass("btn-primary").addClass("btn-secondary");
        $(this).removeClass("btn-secondary").addClass("btn-primary");

        const offset = $(this).data("offset");
        $("#timeOffsetMinutes").val(offset);

        // Show/hide manual datetime field based on whether they click "Custom"
        if (offset === "custom") {
            $("#customTimeContainer").removeClass("d-none");
            $("#timeOffsetMinutes").val(""); // clear offset if custom
        } else {
            $("#customTimeContainer").addClass("d-none");
        }
    });
}

// Display helpful portion recommendations for single-unit items (e.g. eggs, bananas)
function updatePortionHelper(foodName) {
    const $helper = $("#portionHelperText");
    if (!$helper.length) return;
    
    let helperHtml = "";
    const name = foodName.toLowerCase();
    
    if (name.includes("egg")) {
        helperHtml = "<i class='bi bi-info-circle me-1'></i>1 large egg is about <strong>50g</strong> (3 eggs ≈ 150g)";
    } else if (name.includes("sausage")) {
        helperHtml = "<i class='bi bi-info-circle me-1'></i>1 sausage is about <strong>90g</strong> (2 sausages ≈ 180g)";
    } else if (name.includes("dortmunder") || name.includes("dortminder") || name.includes("beer")) {
        helperHtml = "<i class='bi bi-info-circle me-1'></i>1 pint of beer is about <strong>568g/ml</strong> (2 pints ≈ 1136g)";
    } else if (name.includes("mixed nuts")) {
        helperHtml = "<i class='bi bi-info-circle me-1'></i>1/8 of a bag is about <strong>25g</strong>";
    } else if (name.includes("banana")) {
        helperHtml = "<i class='bi bi-info-circle me-1'></i>1 medium banana is about <strong>120g</strong>";
    } else if (name.includes("apple")) {
        helperHtml = "<i class='bi bi-info-circle me-1'></i>1 medium apple is about <strong>150g</strong>";
    } else if (name.includes("bread") || name.includes("toast")) {
        helperHtml = "<i class='bi bi-info-circle me-1'></i>1 slice of bread is about <strong>35-40g</strong>";
    } else if (name.includes("orange")) {
        helperHtml = "<i class='bi bi-info-circle me-1'></i>1 medium orange is about <strong>130g</strong>";
    } else if (name.includes("potato")) {
        helperHtml = "<i class='bi bi-info-circle me-1'></i>1 medium potato is about <strong>150g</strong>";
    } else if (name.includes("date")) {
        helperHtml = "<i class='bi bi-info-circle me-1'></i>1 dried date is about <strong>7-8g</strong>";
    } else if (name.includes("bacon")) {
        helperHtml = "<i class='bi bi-info-circle me-1'></i>1 slice of cooked bacon is about <strong>8g</strong>";
    } else if (name.includes("ham")) {
        helperHtml = "<i class='bi bi-info-circle me-1'></i>1 slice of ham is about <strong>25g</strong> (4 slices ≈ 100g)";
    } else if (name.includes("cheddar") || name.includes("cheese")) {
        helperHtml = "<i class='bi bi-info-circle me-1'></i>1 typical portion of cheese is about <strong>30g</strong>";
    } else if (name.includes("tomato")) {
        helperHtml = "<i class='bi bi-info-circle me-1'></i>1 medium tomato is about <strong>80-90g</strong>";
    } else if (name.includes("onion")) {
        if (name.includes("pickled")) {
            helperHtml = "<i class='bi bi-info-circle me-1'></i>1 portion of pickled onions is about <strong>50g</strong>";
        } else {
            helperHtml = "<i class='bi bi-info-circle me-1'></i>1 medium onion is about <strong>80-90g</strong>";
        }
    } else if (name.includes("olive")) {
        helperHtml = "<i class='bi bi-info-circle me-1'></i>1/4 pack of olives is about <strong>18g</strong>";
    } else if (name.includes("caesar") || name.includes("dressing")) {
        helperHtml = "<i class='bi bi-info-circle me-1'></i>1 tablespoon of dressing is about <strong>15g/ml</strong>";
    }
    
    if (helperHtml) {
        $helper.html(helperHtml).removeClass("d-none");
    } else {
        $helper.addClass("d-none").html("");
    }
}

// Chart.js helper to create a vertical gradient that goes Red above 7.8 mmol/L and Blue/Cyan below
function getGlucoseGradient(chart, ctx, chartArea) {
    const chartHeight = chartArea.bottom - chartArea.top;
    const gradient = ctx.createLinearGradient(0, chartArea.bottom, 0, chartArea.top);
    
    // We need to map the 7.8 threshold to a percentage of the chart height
    // Since chart scale limits vary, we approximate or dynamically compute based on scale
    const yScale = chart.scales.y;
    const minVal = yScale.min;
    const maxVal = yScale.max;
    
    // Safety check for single/zero range
    if (maxVal - minVal <= 0) {
        gradient.addColorStop(0, '#0ea5e9'); // Cyan
        gradient.addColorStop(1, '#ef4444'); // Red
        return gradient;
    }

    const thresholdPercent = (7.8 - minVal) / (maxVal - minVal);
    const clampedPercent = Math.max(0, Math.min(1, thresholdPercent));

    // Gradient transition: blue/cyan at the bottom, going to red at the top above 7.8
    gradient.addColorStop(0, '#06b6d4'); // Cyan at bottom
    gradient.addColorStop(clampedPercent - 0.02, '#0ea5e9'); // Soft Blue right below threshold
    gradient.addColorStop(clampedPercent, '#ef4444'); // Vivid Red at 7.8 threshold
    gradient.addColorStop(1, '#ef4444'); // Red all the way up

    return gradient;
}

// Load data and draw chart
function initializeGlucoseChart(timeframe) {
    // Set active button style for chart range selectors
    $(".chart-time-toggle").removeClass("active");
    $(`.chart-time-toggle[data-range='${timeframe}']`).addClass("active");

    const targetDate = $("#targetDate").val() || "";

    $.ajax({
        url: "/Home/GetGlucoseChartData",
        type: "GET",
        data: { timeframe: timeframe, date: targetDate },
        success: function (data) {
            drawChart(data.points, timeframe);
            updateDashboardStats(data.stats);
        }
    });
}

function drawChart(points, timeframe) {
    const labels = points.map(p => {
        return timeframe === "Last7Days" ? p.displayDate : p.displayTime;
    });
    
    const estValues = points.map(p => p.estimatedValue);
    const actualValues = points.map(p => p.actualValue);

    const maxEst = Math.max(...estValues.filter(v => v !== null), 0);
    const maxAct = Math.max(...actualValues.filter(v => v !== null), 0);
    const calculatedMax = Math.max(9.0, Math.ceil(Math.max(maxEst, maxAct) + 1.0));

    // Find index of the point closest to the current time (only for Today or Last24Hours)
    let closestIndex = -1;
    if (timeframe === "Today" || timeframe === "Last24Hours") {
        const now = new Date();
        let minDiff = Infinity;
        points.forEach((p, idx) => {
            const pDate = new Date(p.time);
            const diff = Math.abs(now - pDate);
            if (diff < minDiff) {
                minDiff = diff;
                closestIndex = idx;
            }
        });
        // Only show "Now" if it's within 15 minutes of the point's range
        if (minDiff > 15 * 60 * 1000) {
            closestIndex = -1;
        }
    }

    const canvas = document.getElementById("glucoseChart");
    if (!canvas) return;

    const ctx = canvas.getContext("2d");

    // Destroy existing chart if it exists
    if (glucoseChart !== null) {
        glucoseChart.destroy();
    }

    // Configure dataset styling
    const datasets = [
        {
            label: "Estimated Glucose (mmol/L)",
            data: estValues,
            borderColor: function(context) {
                const chart = context.chart;
                const {ctx, chartArea} = chart;
                if (!chartArea) return null;
                return getGlucoseGradient(chart, ctx, chartArea);
            },
            borderWidth: 3,
            pointRadius: 0, // continuous smooth line
            pointHoverRadius: 6,
            fill: true,
            // Gradient fill under the curve (fade to transparent)
            backgroundColor: function(context) {
                const chart = context.chart;
                const {ctx, chartArea} = chart;
                if (!chartArea) return null;
                
                const gradient = ctx.createLinearGradient(0, chartArea.bottom, 0, chartArea.top);
                gradient.addColorStop(0, 'rgba(14, 165, 233, 0.01)');
                gradient.addColorStop(0.5, 'rgba(14, 165, 233, 0.05)');
                gradient.addColorStop(1, 'rgba(239, 68, 68, 0.15)');
                return gradient;
            },
            tension: 0.35 // smooth line
        },
        {
            label: "Actual Readings (mmol/L)",
            data: actualValues,
            type: "scatter",
            backgroundColor: function(context) {
                const val = context.raw;
                return val >= 7.8 ? '#ef4444' : '#10b981'; // Red for spikes, green for normal
            },
            borderColor: '#ffffff',
            borderWidth: 2,
            pointRadius: 6,
            pointHoverRadius: 8,
            showLine: false
        }
    ];

    // Chart Configuration
    glucoseChart = new Chart(ctx, {
        type: 'line',
        data: {
            labels: labels,
            datasets: datasets
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            interaction: {
                mode: 'index',
                intersect: false
            },
            plugins: {
                legend: {
                    labels: {
                        color: '#9ca3af',
                        font: {
                            family: "'Inter', sans-serif",
                            weight: '500'
                        }
                    }
                },
                tooltip: {
                    backgroundColor: '#192134',
                    titleColor: '#f3f4f6',
                    bodyColor: '#f3f4f6',
                    borderColor: 'rgba(255,255,255,0.1)',
                    borderWidth: 1,
                    padding: 12,
                    callbacks: {
                        label: function(context) {
                            let label = context.dataset.label || '';
                            if (label) {
                                label += ': ';
                            }
                            if (context.raw !== null) {
                                label += context.raw + ' mmol/L';
                            }
                            return label;
                        }
                    }
                }
            },
            scales: {
                x: {
                    grid: {
                        color: 'rgba(255,255,255,0.03)',
                        drawTicks: false
                    },
                    ticks: {
                        color: '#9ca3af',
                        maxTicksLimit: timeframe === "Last24Hours" ? 12 : 8
                    }
                },
                y: {
                    min: 3.0,
                    max: calculatedMax,
                    grid: {
                        color: 'rgba(255,255,255,0.04)',
                        drawTicks: false
                    },
                    ticks: {
                        color: '#9ca3af'
                    }
                }
            }
        },
        plugins: [{
            // Custom plugin to draw threshold line and vertical now line
            id: 'thresholdLine',
            afterDraw: function(chart) {
                const {ctx, chartArea, scales: {y}} = chart;
                const yPixel = y.getPixelForValue(7.8);

                // Draw vertical "Now" line if applicable
                if (closestIndex !== -1) {
                    const xPixel = chart.scales.x.getPixelForValue(chart.data.labels[closestIndex]);
                    if (xPixel >= chartArea.left && xPixel <= chartArea.right) {
                        ctx.save();
                        
                        // Draw dashed line
                        ctx.strokeStyle = 'rgba(245, 158, 11, 0.6)'; // Amber color with opacity
                        ctx.lineWidth = 2;
                        ctx.setLineDash([4, 4]);
                        ctx.beginPath();
                        ctx.moveTo(xPixel, chartArea.top);
                        ctx.lineTo(xPixel, chartArea.bottom);
                        ctx.stroke();

                        // Draw "NOW" text
                        ctx.fillStyle = '#f59e0b'; // Solid Amber
                        ctx.font = '600 10px Montserrat';
                        ctx.textAlign = 'center';
                        ctx.fillText('NOW', xPixel, chartArea.top + 15);
                        
                        ctx.restore();
                    }
                }
                
                if (yPixel >= chartArea.top && yPixel <= chartArea.bottom) {
                    ctx.save();
                    
                    // Draw dashed limit line
                    ctx.strokeStyle = 'rgba(239, 68, 68, 0.4)';
                    ctx.lineWidth = 2;
                    ctx.setLineDash([6, 4]);
                    ctx.beginPath();
                    ctx.moveTo(chartArea.left, yPixel);
                    ctx.lineTo(chartArea.right, yPixel);
                    ctx.stroke();

                    // Draw "Spike Limit" label on the chart
                    ctx.fillStyle = '#ef4444';
                    ctx.font = '600 11px Montserrat';
                    ctx.textAlign = 'right';
                    ctx.fillText('Spike Limit (7.8 mmol/L)', chartArea.right - 10, yPixel - 6);
                    
                    ctx.restore();
                }
            }
        }]
    });
}

// Dynamically updates dashboard stats card values from AJAX response
function updateDashboardStats(stats) {
    if (!stats) return;

    $("#statCurrentGlucose").text(stats.currentGlucose.toFixed(1));
    $("#statPeakGlucose").text(stats.peakGlucoseToday.toFixed(1));
    $("#statTIR").text(stats.timeInRangePercentage.toFixed(0) + "%");
    $("#statCarbs").text(stats.totalCarbsToday.toFixed(0) + "g");
    $("#statAvgGI").text(stats.averageGiToday.toFixed(0));
    $("#statRedZone").text(stats.minutesInRedZoneToday + "m");

    // Dynamic warning styling on current glucose card
    const $currCard = $("#currentGlucoseCard");
    $currCard.removeClass("accent-red accent-cyan accent-orange");
    if (stats.currentGlucose >= 7.8) {
        $currCard.addClass("accent-red");
    } else if (stats.currentGlucose >= 6.1) {
        $currCard.addClass("accent-orange");
    } else {
        $currCard.addClass("accent-cyan");
    }

    // Dynamic warning styling on peak glucose card
    const $peakCard = $("#peakGlucoseCard");
    $peakCard.removeClass("accent-red accent-cyan");
    if (stats.peakGlucoseToday >= 7.8) {
        $peakCard.addClass("accent-red");
    } else {
        $peakCard.addClass("accent-cyan");
    }
}
