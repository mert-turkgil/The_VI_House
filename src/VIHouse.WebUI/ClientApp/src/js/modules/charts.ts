import {
  Chart,
  BarController,
  BarElement,
  CategoryScale,
  DoughnutController,
  Filler,
  Legend,
  LineController,
  LineElement,
  LinearScale,
  PointElement,
  Tooltip,
  ArcElement,
} from 'chart.js';

Chart.register(
  BarController, BarElement, DoughnutController, ArcElement, LineController, LineElement,
  PointElement, CategoryScale, LinearScale, Filler, Legend, Tooltip,
);

const GREEN = '#00230a';
const GOLD = '#b8955a';
const GOLD_LIGHT = '#d7bd8a';
const INK_MUTED = 'rgba(18, 22, 15, 0.55)';

// Every status/currency series shares one ordered palette so the same category keeps the same
// colour between the donut and the bars on a single screen.
const SERIES_COLOURS = [GREEN, GOLD, '#1c4229', GOLD_LIGHT, '#0e3018', '#c7c9c2', '#6b7a63'];

interface ChartPayload {
  labels: string[];
  values: number[];
  /** Present on money series only; formats the axis and tooltips as currency. */
  currency?: string;
}

/** Reads a chart's data from the `<canvas data-chart-payload="...">` JSON the Razor view emits. */
function readPayload(canvas: HTMLCanvasElement): ChartPayload | null {
  const raw = canvas.dataset.chartPayload;
  if (!raw) return null;

  try {
    const parsed = JSON.parse(raw) as ChartPayload;
    return Array.isArray(parsed.labels) && Array.isArray(parsed.values) ? parsed : null;
  } catch {
    return null;
  }
}

function moneyFormatter(currency?: string): (value: number) => string {
  if (!currency) return (value) => value.toLocaleString('en-GB');

  const format = new Intl.NumberFormat('en-GB', {
    style: 'currency',
    currency,
    maximumFractionDigits: 0,
  });
  return (value) => format.format(value);
}

/**
 * Renders the admin dashboard charts. Each `<canvas data-chart="line|bar|doughnut">` carries its own
 * data as JSON, so the controller stays the single source of truth for the numbers and this module
 * never queries anything itself.
 */
export function initAdminCharts(): void {
  const canvases = Array.from(document.querySelectorAll<HTMLCanvasElement>('canvas[data-chart]'));
  if (canvases.length === 0) return;

  Chart.defaults.font.family = "'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif";
  Chart.defaults.color = INK_MUTED;

  canvases.forEach((canvas) => {
    const payload = readPayload(canvas);
    if (!payload || payload.values.length === 0) return;

    const kind = canvas.dataset.chart as 'line' | 'bar' | 'doughnut';
    const formatValue = moneyFormatter(payload.currency);

    if (kind === 'doughnut') {
      new Chart(canvas, {
        type: 'doughnut',
        data: {
          labels: payload.labels,
          datasets: [{
            data: payload.values,
            backgroundColor: payload.labels.map((_, i) => SERIES_COLOURS[i % SERIES_COLOURS.length]),
            borderWidth: 0,
          }],
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          cutout: '62%',
          plugins: { legend: { position: 'right', labels: { boxWidth: 10, usePointStyle: true } } },
        },
      });
      return;
    }

    new Chart(canvas, {
      type: kind === 'bar' ? 'bar' : 'line',
      data: {
        labels: payload.labels,
        datasets: [{
          data: payload.values,
          label: canvas.dataset.chartLabel ?? '',
          borderColor: GOLD,
          backgroundColor: kind === 'bar' ? GREEN : 'rgba(184, 149, 90, 0.14)',
          borderWidth: 2,
          fill: kind === 'line',
          tension: 0.35,
          pointRadius: 3,
          pointBackgroundColor: GOLD,
          borderRadius: kind === 'bar' ? 4 : undefined,
        }],
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { display: false },
          tooltip: { callbacks: { label: (ctx) => formatValue(Number(ctx.parsed.y)) } },
        },
        scales: {
          x: { grid: { display: false } },
          y: {
            beginAtZero: true,
            border: { display: false },
            ticks: { callback: (value) => formatValue(Number(value)) },
          },
        },
      },
    });
  });
}
