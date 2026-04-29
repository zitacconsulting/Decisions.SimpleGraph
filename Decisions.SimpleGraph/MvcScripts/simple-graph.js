/**
 * $DP.Control.SimpleGraphControl
 *
 * SVG area/line chart for the Decisions platform.
 * Mirrors the "Running Hours – Last 14 Days" chart style from the reference dashboard.
 *
 * Design-time options (this.options.*):
 *   dataName          {string}   — data name for Y-value double[]
 *   staticInput       {boolean}  — Decisions native flag: true = use staticDataJson, false = use flow data
 *   staticDataJson    {string}   — JSON double[] used when staticInput is true
 *   xLabelsDataName   {string}   — data name for X-label string[]
 *   xLabelsFromData   {boolean}  — when true, X labels come from flow
 *   staticXLabelsJson {string}   — JSON string[] used when xLabelsFromData is false
 *   color             {string}   — hex color for line, dots, fill, avg line
 *   yAxisSuffix       {string}   — appended to every Y-axis tick label (e.g. "h", "%")
 *   showAverageLine   {boolean}  — dashed horizontal line at the dataset average
 *   showDataPoints    {boolean}  — filled circles at each data point
 *   showAreaFill      {boolean}  — gradient fill under the line
 *
 * X-label thinning: labels are skipped automatically when there are more data points
 * than can comfortably fit. The last label is always shown. The skip factor grows with n:
 *   n ≤ 10  → every label
 *   n ≤ 20  → every 2nd
 *   n ≤ 40  → every 4th
 *   n > 40  → ceil(n/8)
 */

$DP         = $DP         || {};
$DP.Control = $DP.Control || {};

$DP.Control.SimpleGraphControl = class SimpleGraphControl
    extends $DP.Control.SilverPart {

    // ── Constructor ──────────────────────────────────────────────────────────────

    constructor($controlLayout, options) {
        super($controlLayout, options);
        this._uid            = 'sgc-' + Date.now() + '-' + Math.floor(Math.random() * 1e5);
        this._container      = null;   // inner div where the SVG is injected
        this._runtimeData    = null;   // double[] received via setValue
        this._runtimeLabels  = null;   // string[] received via setValue
        this._currentWidth   = 0;
        this._currentHeight  = 0;
        this._ro             = null;   // ResizeObserver
    }

    // ── Rendering ────────────────────────────────────────────────────────────────

    renderhtml(host) {
        const h = (this.options && this.options.graphHeight) || 300;

        const $root = $('<div>')
            .addClass('sgc-root')
            .css({ position: 'relative', width: '100%', height: h + 'px',
                   overflow: 'hidden', fontFamily: 'Arial,sans-serif',
                   boxSizing: 'border-box' });

        const $inner = $('<div>')
            .attr('id', this._uid + '-inner')
            .css({ width: '100%', height: '100%' });

        $root.append($inner);
        this._container = $inner[0];

        if (typeof ResizeObserver !== 'undefined') {
            this._ro = new ResizeObserver(entries => {
                const r = entries[0].contentRect;
                const w = Math.floor(r.width);
                const h = Math.floor(r.height);
                if (w > 0 && h > 0 && (w !== this._currentWidth || h !== this._currentHeight)) {
                    this._currentWidth  = w;
                    this._currentHeight = h;
                    this._render();
                }
            });
            // Observe after the element is attached to the DOM
            setTimeout(() => { if (this._ro) this._ro.observe($root[0]); }, 0);
        }

        this._render();
        return $root;
    }

    getControl() {
        return this.$controlLayout ? this.$controlLayout.find('.sgc-root') : $();
    }

    resize(height, width) {
        // Fallback for environments without ResizeObserver
        if (!this._ro && height > 0 && width > 0) {
            this._currentHeight = height;
            this._currentWidth  = width;
            if (this.$controlLayout)
                this.$controlLayout.find('.sgc-root').css({ width, height });
            this._render();
        }
    }

    // ── Data I/O ──────────────────────────────────────────────────────────────────

    /**
     * Called by Decisions when data flows into the form.
     * Handles both the Y-series stream and the X-labels stream.
     */
    setValue(data, isFromStartUp) {
        if (!data) return;
        const opts     = this.options || {};
        const dataName = opts.dataName;
        const labName  = opts.xLabelsDataName;

        const items = Array.isArray(data) ? data
            : (typeof data.toArray === 'function' ? data.toArray() : []);

        if (dataName) {
            const found = items.find(t => t && t.name === dataName);
            if (found && found.value != null) {
                const val = found.value;
                let parsed;
                if (typeof val === 'string') {
                    try { parsed = JSON.parse(val); } catch (e) { parsed = []; }
                } else {
                    parsed = val;
                }
                this._runtimeData = Array.isArray(parsed)
                    ? parsed.map(Number).filter(v => !isNaN(v))
                    : [];
            }
        }

        if (labName) {
            const found = items.find(t => t && t.name === labName);
            if (found && found.value != null) {
                const val = found.value;
                let parsed;
                if (typeof val === 'string') {
                    try { parsed = JSON.parse(val); } catch (e) { parsed = []; }
                } else {
                    parsed = val;
                }
                this._runtimeLabels = Array.isArray(parsed) ? parsed.map(String) : [];
            }
        }

        this._render();
    }

    /** Display-only control — returns nothing meaningful. */
    getValue() {
        return [];
    }

    // ── Active data accessors ─────────────────────────────────────────────────────

    _getData() {
        const opts = this.options || {};
        if (!opts.staticInput && this._runtimeData !== null) return this._runtimeData;
        const json = opts.staticDataJson;
        if (typeof json === 'string') {
            try {
                const p = JSON.parse(json);
                if (Array.isArray(p)) return p.map(Number).filter(v => !isNaN(v));
            } catch (e) {}
        }
        return [];
    }

    _getLabels() {
        const opts = this.options || {};
        if (opts.xLabelsFromData && this._runtimeLabels !== null) return this._runtimeLabels;
        const json = opts.staticXLabelsJson;
        if (typeof json === 'string') {
            try {
                const p = JSON.parse(json);
                if (Array.isArray(p)) return p.map(String);
            } catch (e) {}
        }
        return [];
    }

    // ── Chart rendering ───────────────────────────────────────────────────────────

    _render() {
        if (!this._container) return;

        const data   = this._getData();
        const labels = this._getLabels();
        const opts   = this.options || {};

        const color      = opts.color           || '#3d6fb5';
        const suffix     = opts.yAxisSuffix     || '';
        const showAvg    = opts.showAverageLine  !== false;
        const showDots   = opts.showDataPoints   !== false;
        const showFill   = opts.showAreaFill     !== false;

        // Use the pixel dimensions supplied by resize(); fall back to the container's
        // measured size for the very first paint before resize fires.
        const W = this._currentWidth  || this._container.offsetWidth  || 400;
        const H = this._currentHeight || this._container.offsetHeight || 200;
        const pL = 44;  // left  — room for Y-axis labels
        const pR = 8;   // right
        const pT = 10;  // top
        const pB = 28;  // bottom — room for X-axis labels

        const cW = W - pL - pR;
        const cH = H - pT - pB;

        const n = data.length;

        if (n === 0) {
            this._container.innerHTML =
                '<div style="width:' + W + 'px;height:' + H + 'px;display:flex;align-items:center;' +
                'justify-content:center;color:#aaa;font-size:13px;">No data</div>';
            return;
        }

        const dataMax = Math.max(...data);
        const max     = dataMax > 0 ? dataMax : 1;  // guard against all-zero series
        const gradId  = this._uid + '-grad';

        // gx: maps data index to SVG X coordinate
        const gx = i => pL + (n > 1 ? (i / (n - 1)) * cW : cW / 2);
        // gy: maps a Y value to SVG Y coordinate (0 at bottom, max at top)
        const gy = v => pT + cH - (Math.max(0, v) / max) * cH;

        // ── Y axis ───────────────────────────────────────────────────────────────
        // Three horizontal grid lines: 0, mid, max
        const yTicks = [0, max / 2, max];
        const grids  = yTicks.map(v => {
            const yr    = Math.round(v * 10) / 10;  // 1 decimal place
            const label = Number.isInteger(yr) ? String(yr) : yr.toFixed(1);
            return '<line x1="' + pL + '" y1="' + gy(v) + '" x2="' + (W - pR) + '" y2="' + gy(v) + '" stroke="#eee" stroke-dasharray="3,3"/>' +
                   '<text x="' + (pL - 3) + '" y="' + (gy(v) + 3.5) + '" text-anchor="end" font-size="10" fill="#888">' + this._esc(label + suffix) + '</text>';
        }).join('');

        // ── Average line ─────────────────────────────────────────────────────────
        const avg    = data.reduce((a, b) => a + b, 0) / n;
        const avgLine = showAvg
            ? '<line x1="' + pL + '" y1="' + gy(avg) + '" x2="' + (W - pR) + '" y2="' + gy(avg) + '" stroke="' + color + '" stroke-dasharray="4,3" stroke-width="1" opacity=".4"/>'
            : '';

        // ── X axis labels with skip logic ─────────────────────────────────────────
        const skip = n <= 10 ? 1 : n <= 20 ? 2 : n <= 40 ? 4 : Math.ceil(n / 8);
        const xLabels = data.map((_, i) => {
            if (i % skip !== 0 && i !== n - 1) return '';
            const lbl = (labels[i] != null && labels[i] !== '') ? labels[i] : String(i + 1);
            return '<text x="' + gx(i) + '" y="' + (H - 4) + '" text-anchor="middle" font-size="10" fill="#888">' + this._esc(lbl) + '</text>';
        }).join('');

        // ── Path data ─────────────────────────────────────────────────────────────
        const ptPairs = data.map((v, i) => gx(i) + ',' + gy(v));
        const lineD   = 'M ' + ptPairs.join(' L ');
        const areaD   = 'M ' + gx(0) + ',' + (pT + cH) + ' L ' + ptPairs.join(' L ') + ' L ' + gx(n - 1) + ',' + (pT + cH) + ' Z';

        // ── Dots ──────────────────────────────────────────────────────────────────
        const dots = showDots
            ? data.map((v, i) => '<circle cx="' + gx(i) + '" cy="' + gy(v) + '" r="2.5" fill="' + color + '" opacity=".85"/>').join('')
            : '';

        // ── Axes ──────────────────────────────────────────────────────────────────
        const yAxis = '<line x1="' + pL + '" y1="' + pT + '" x2="' + pL + '" y2="' + (pT + cH) + '" stroke="#ddd"/>';
        const xAxis = '<line x1="' + pL + '" y1="' + (pT + cH) + '" x2="' + (W - pR) + '" y2="' + (pT + cH) + '" stroke="#ddd"/>';

        // ── SVG assembly ──────────────────────────────────────────────────────────
        const svg =
            '<svg viewBox="0 0 ' + W + ' ' + H + '" width="' + W + '" height="' + H + '" xmlns="http://www.w3.org/2000/svg" style="display:block">' +
            '<defs>' +
              '<linearGradient id="' + gradId + '" x1="0" y1="0" x2="0" y2="1">' +
                '<stop offset="0%" stop-color="' + color + '" stop-opacity=".18"/>' +
                '<stop offset="100%" stop-color="' + color + '" stop-opacity="0"/>' +
              '</linearGradient>' +
            '</defs>' +
            grids +
            avgLine +
            (showFill ? '<path d="' + areaD + '" fill="url(#' + gradId + ')"/>' : '') +
            '<path d="' + lineD + '" fill="none" stroke="' + color + '" stroke-width="2" stroke-linejoin="round" stroke-linecap="round"/>' +
            dots +
            xLabels +
            yAxis +
            xAxis +
            '</svg>';

        this._container.innerHTML = svg;
    }

    // ── Utility ───────────────────────────────────────────────────────────────────

    _esc(str) {
        return String(str || '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }
};
