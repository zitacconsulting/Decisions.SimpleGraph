using System;
using System.Collections.Generic;
using System.Linq;
using DecisionsFramework;
using Decisions.Silverlight.UI.Core.FormDesignerModel;
using DecisionsFramework.ComponentData;
using DecisionsFramework.Design.ConfigurationStorage.Attributes;
using DecisionsFramework.Design.Flow.Mapping;
using DecisionsFramework.Design.Properties;
using DecisionsFramework.Design.Properties.Attributes;
using DecisionsFramework.ServiceLayer;
using DecisionsFramework.ServiceLayer.Services.ConfigurationStorage;
using DecisionsFramework.ServiceLayer.Services.ContextData;
using DecisionsFramework.ServiceLayer.Utilities;
using DecisionsFramework.Utilities.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Silverdark.Components;

namespace Decisions.SimpleGraph
{
    // ── Toolbox registrar ────────────────────────────────────────────────────────

    public class SimpleGraphControlInitializer
        : IBootInitializable, IInitializable, IInializableOrder
    {
        public int Ordinal => 10000;

        public InitializableHost[] Environments => new[]
        {
            InitializableHost.Unmanaged,
            InitializableHost.User,
            InitializableHost.Control,
        };

        public InitializablePhase Phase => InitializablePhase.ApplicationBoot;

        public string Name => "Register Simple Graph Control";

        public void Initialize()
        {
            ConfigurationStorageService.RegisterModulesToolboxElement(
                "Simple Graph",
                typeof(SimpleGraphControl).AssemblyQualifiedName!,
                "Charts",
                "Decisions.SimpleGraph",
                ElementType.FormElement);
        }
    }

    // ── Control ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Display-only SVG area/line chart form control.
    ///
    /// Y values (double[]) and X labels (string[]) can each be supplied from flow
    /// data at runtime or configured statically at design time.
    ///
    /// Y data source: follows the standard Decisions convention —
    ///   Static Input = false (default) → data arrives via DataName from the flow.
    ///   Static Input = true            → StaticData array configured in the designer.
    ///
    /// X labels source: XLabelsFromData toggle selects between XLabelsDataName and
    /// StaticXLabels (no built-in base-class parallel for a secondary data stream).
    ///
    /// X labels are automatically thinned when the number of data points exceeds the
    /// available horizontal space (same behaviour as the reference dashboard chart).
    /// </summary>
    [Writable]
    public class SimpleGraphControl
        : DataContentBase<SimpleGraphControl, double[]>
    {
        // ── Private state ─────────────────────────────────────────────────────────

        private double[] _data           = Array.Empty<double>();
        private double[] _staticData     = Array.Empty<double>();

        private bool     _xLabelsFromData;
        private string   _xLabelsDataName = string.Empty;
        private string[] _staticXLabels   = Array.Empty<string>();
        private string[] _xLabels         = Array.Empty<string>();

        private string   _color           = "#3d6fb5";
        private string   _yAxisSuffix     = string.Empty;
        private bool     _showAverageLine = true;
        private bool     _showDataPoints  = true;
        private bool     _showAreaFill    = true;
        private int      _graphHeight     = 300;

        // ── Data Source ───────────────────────────────────────────────────────────
        // StaticInput (from Common Properties) is the Decisions-native toggle:
        //   false → Y values come from the flow via DataName
        //   true  → Y values are configured statically via StaticData below

        [ClientOption][WritableValue]
        [PropertyHiddenByValue(nameof(StaticInput), true, true)]
        public override string DataName
        {
            get => base.DataName;
            set => base.DataName = value;
        }

        [WritableValue][ClientOption][PropertyClassification(1, "Static Data", "Common Properties")]
        [PropertyHiddenByValue(nameof(StaticInput), false, true)]
        public double[] StaticData
        {
            get => _staticData;
            set { _staticData = value ?? Array.Empty<double>(); OnPropertyChanged(nameof(StaticData)); }
        }

        // ── Appearance ────────────────────────────────────────────────────────────

        [WritableValue][ClientOption][ColorPickerEditor][PropertyClassification(0, "Color", "Appearance")]
        public string Color
        {
            get => _color;
            set { _color = string.IsNullOrWhiteSpace(value) ? "#3d6fb5" : value; OnPropertyChanged(nameof(Color)); }
        }

        [WritableValue][ClientOption][PropertyClassification(1, "Y Axis Suffix", "Appearance")]
        public string YAxisSuffix
        {
            get => _yAxisSuffix;
            set { _yAxisSuffix = value ?? string.Empty; OnPropertyChanged(nameof(YAxisSuffix)); }
        }

        [WritableValue][ClientOption][PropertyClassification(2, "Show Average Line", "Appearance")]
        public bool ShowAverageLine
        {
            get => _showAverageLine;
            set { _showAverageLine = value; OnPropertyChanged(nameof(ShowAverageLine)); }
        }

        [WritableValue][ClientOption][PropertyClassification(3, "Show Data Points", "Appearance")]
        public bool ShowDataPoints
        {
            get => _showDataPoints;
            set { _showDataPoints = value; OnPropertyChanged(nameof(ShowDataPoints)); }
        }

        [WritableValue][ClientOption][PropertyClassification(4, "Show Area Fill", "Appearance")]
        public bool ShowAreaFill
        {
            get => _showAreaFill;
            set { _showAreaFill = value; OnPropertyChanged(nameof(ShowAreaFill)); }
        }

        // ── Layout ────────────────────────────────────────────────────────────────

        [WritableValue][ClientOption][PropertyClassification(0, "Graph Height (px)", "Layout")]
        public int GraphHeight
        {
            get => _graphHeight;
            set { _graphHeight = value < 60 ? 60 : value; OnPropertyChanged(nameof(GraphHeight)); }
        }

        [WritableValue][ClientOption][PropertyClassification(5, "X Labels From Data", "Appearance")]
        public bool XLabelsFromData
        {
            get => _xLabelsFromData;
            set { _xLabelsFromData = value; OnPropertyChanged(nameof(XLabelsFromData)); }
        }

        [WritableValue][ClientOption][PropertyClassification(6, "X Labels Data Name", "Appearance")]
        [PropertyHiddenByValue(nameof(XLabelsFromData), false, true)]
        public string XLabelsDataName
        {
            get => _xLabelsDataName;
            set { _xLabelsDataName = value ?? string.Empty; OnPropertyChanged(nameof(XLabelsDataName)); }
        }

        [WritableValue][ClientOption][PropertyClassification(7, "Static X Labels", "Appearance")]
        [PropertyHiddenByValue(nameof(XLabelsFromData), true, true)]
        public string[] StaticXLabels
        {
            get => _staticXLabels;
            set { _staticXLabels = value ?? Array.Empty<string>(); OnPropertyChanged(nameof(StaticXLabels)); }
        }

        // ── Pre-serialised JSON for JS ────────────────────────────────────────────
        // [ClientOption] on complex [Writable] arrays only emits $type metadata.
        // Pre-serialise to plain JSON strings so the JS side gets actual values.

        [ClientOption][PropertyHidden]
        public string StaticDataJson =>
            JsonConvert.SerializeObject(_staticData ?? Array.Empty<double>());

        [ClientOption][PropertyHidden]
        public string StaticXLabelsJson =>
            JsonConvert.SerializeObject(_staticXLabels ?? Array.Empty<string>());

        // ── DataContentBase overrides ─────────────────────────────────────────────

        protected override double[] GetValue() => _data;

        protected override bool SetValue(double[] value)
        {
            _data = value ?? Array.Empty<double>();
            return true;
        }

        protected override double[] GetConvertedValue(object value) => value switch
        {
            double[]            arr  => arr,
            IEnumerable<double> en   => en.ToArray(),
            JArray              jarr => jarr.ToObject<double[]>() ?? Array.Empty<double>(),
            string              s    => ParseDoubleArray(s),
            _                        => Array.Empty<double>(),
        };

        public override void SetControlValue(FormDataDictionary dataDictionary)
        {
            if (dataDictionary == null) return;

            if (!string.IsNullOrEmpty(DataName) && dataDictionary.ContainsKey(DataName))
                SetValue(ToDoubleArray(dataDictionary[DataName]));

            if (!string.IsNullOrEmpty(_xLabelsDataName) && dataDictionary.ContainsKey(_xLabelsDataName))
                _xLabels = ToStringArray(dataDictionary[_xLabelsDataName]);
        }

        public override void ConsumeData(IDictionary<string, object> data)
        {
            base.ConsumeData(data);

            if (!string.IsNullOrEmpty(_xLabelsDataName) && data.TryGetValue(_xLabelsDataName, out var rawX) && rawX != null)
            {
                var labels = ToStringArray(rawX);
                if (labels.Length > 0) _xLabels = labels;
            }
        }

        public override DataPair[] ControlValues
        {
            get
            {
                // base.ControlValues already adds DataName → _data (the actual double[])
                // via GetValue(). Passing the typed array lets both the AFF type system
                // and the JS setValue work correctly. Never replace it with a JSON string.
                var pairs = base.ControlValues.ToList();

                if (!string.IsNullOrEmpty(_xLabelsDataName))
                {
                    var idx  = pairs.FindIndex(p => p.Name == _xLabelsDataName);
                    var pair = new DataPair(_xLabelsDataName, _xLabels);
                    if (idx >= 0) pairs[idx] = pair; else pairs.Add(pair);
                }

                return pairs.ToArray();
            }
        }

        public override DataDescription[] InputData
        {
            get
            {
                var list = new List<DataDescription>();

                if (!StaticInput && !OutputOnly && !string.IsNullOrEmpty(DataName))
                    list.Add(new DataDescription(typeof(double), DataName, isList: true));

                if (XLabelsFromData && !string.IsNullOrEmpty(_xLabelsDataName))
                    list.Add(new DataDescription(typeof(string), _xLabelsDataName, isList: true));

                return list.ToArray();
            }
        }

        public override OutcomeScenarioData[] OutcomeScenarios
        {
            get
            {
                bool hasSeries = !StaticInput && !string.IsNullOrEmpty(DataName);
                bool hasLabels = XLabelsFromData && !string.IsNullOrEmpty(_xLabelsDataName);

                if (!hasSeries && !hasLabels) return Array.Empty<OutcomeScenarioData>();

                // Paths the user has explicitly promoted to Required or Optional.
                var activePaths = (RequiredOnOutputs ?? Array.Empty<string>())
                    .Concat(OptionalOnOutputs ?? Array.Empty<string>())
                    .ToArray();

                // Paths marked Not Used — already configured, no data emitted.
                var notUsedPaths = NotUsedOnOutputs ?? Array.Empty<string>();

                // Any path not yet touched by the user defaults to Not Used so the
                // designer shows no error without forcing the user to configure anything.
                var allKnownPaths = activePaths.Concat(notUsedPaths).ToHashSet();
                var defaultNotUsed = (OutcomePathsWithOutcomeType ?? Array.Empty<OutcomeScenario>())
                    .Select(o => o.OutcomePath)
                    .Where(n => !allKnownPaths.Contains(n))
                    .ToArray();

                var seriesDesc = hasSeries
                    ? new DataDescription(typeof(double), DataName, isList: true)
                    : null;
                var labelsDesc = hasLabels
                    ? new DataDescription(typeof(string), _xLabelsDataName, isList: true)
                    : null;

                var results = new List<OutcomeScenarioData>();

                // Active paths carry the actual data descriptions.
                foreach (var path in activePaths)
                {
                    if (hasSeries && hasLabels)
                        results.Add(new OutcomeScenarioData(path, seriesDesc!, labelsDesc!));
                    else if (hasSeries)
                        results.Add(new OutcomeScenarioData(path, seriesDesc!));
                    else
                        results.Add(new OutcomeScenarioData(path, labelsDesc!));
                }

                // Not Used paths (both explicit and defaulted) — empty data descriptions.
                // Must pass Array.Empty<DataDescription>() so OutputData returns [] not null,
                // otherwise OutcomeDataNames crashes when it calls .Select() on the null array.
                foreach (var path in notUsedPaths.Concat(defaultNotUsed))
                    results.Add(new OutcomeScenarioData(path, Array.Empty<DataDescription>()));

                return results.ToArray();
            }
        }

        // Suppress the designer validation error that fires when all outcome paths
        // are set to Not Used. Outputting data is optional for this control.
        public override ValidationIssue[] GetValidationIssues() =>
            base.GetValidationIssues()
                .Where(v => v.ReferenceProperty != "OutcomePathsWithOutcomeType")
                .ToArray();

        public override Dictionary<string, object> ProduceData(string outcomePath)
        {
            var dict = new Dictionary<string, object>();

            bool pathOk = (RequiredOnOutputs != null && Array.IndexOf(RequiredOnOutputs, outcomePath) >= 0)
                       || (OptionalOnOutputs != null && Array.IndexOf(OptionalOnOutputs, outcomePath) >= 0);

            if (!pathOk) return dict;

            if (!StaticInput && !string.IsNullOrEmpty(DataName))
                dict[DataName] = _data;

            if (XLabelsFromData && !string.IsNullOrEmpty(_xLabelsDataName))
                dict[_xLabelsDataName] = _xLabels;

            return dict;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static double[] ToDoubleArray(object? raw) => raw switch
        {
            double[]            arr  => arr,
            IEnumerable<double> en   => en.ToArray(),
            JArray              jarr => jarr.ToObject<double[]>() ?? Array.Empty<double>(),
            string              s    => ParseDoubleArray(s),
            _                        => Array.Empty<double>(),
        };

        private static double[] ParseDoubleArray(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return Array.Empty<double>();
            try   { return JsonConvert.DeserializeObject<double[]>(s) ?? Array.Empty<double>(); }
            catch { return Array.Empty<double>(); }
        }

        private static string[] ToStringArray(object? raw) => raw switch
        {
            string[]            arr  => arr,
            IEnumerable<string> en   => en.ToArray(),
            JArray              jarr => jarr.ToObject<string[]>() ?? Array.Empty<string>(),
            string              s    => ParseStringArray(s),
            _                        => Array.Empty<string>(),
        };

        private static string[] ParseStringArray(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return Array.Empty<string>();
            try   { return JsonConvert.DeserializeObject<string[]>(s) ?? Array.Empty<string>(); }
            catch { return Array.Empty<string>(); }
        }
    }
}
