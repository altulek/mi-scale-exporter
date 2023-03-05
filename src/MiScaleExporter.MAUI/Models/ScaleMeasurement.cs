using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiScaleExporter.Models
{
    public class ScaleMeasurement : INotifyPropertyChanged
    {

        private ScaleMeasurement() { }
        public static ScaleMeasurement Instance { get; } = new ScaleMeasurement();

        private List<BodyComposition> _history = new List<BodyComposition>();
        public List<BodyComposition> History
        {
            get => _history;
            set
            {
                if (_history != value)
                {
                    _history = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(History)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanSaveHistory)));
                }
            }
        }

        public bool CanSaveHistory
        {
            get { return _history.Where(h => h.Send == true).Count() > 0; }  
        }


        private string _weight;
        public string Weight
        {
            get => _weight;
            set
            {
                if (_weight != value)
                {
                    _weight = value;
                    PropertyChanged?.Invoke(this,
                        new PropertyChangedEventArgs(nameof(Weight)));
                }
            }
        }

        private string _foundScale;
        public string FoundScale
        {
            get => _foundScale;
            set
            {
                if (_foundScale != value)
                {
                    _foundScale = value;
                    PropertyChanged?.Invoke(this,
                        new PropertyChangedEventArgs(nameof(FoundScale)));
                }
            }
        }

        private string _debugData;
        public string DebugData
        {
            get => _debugData;
            set
            {
                if (_debugData != value)
                {
                    _debugData = value;
                    PropertyChanged?.Invoke(this,
                        new PropertyChangedEventArgs(nameof(DebugData)));
                }
            }
        }

        private string _rawData;
        public string RawData
        {
            get => _rawData;
            set
            {
                if (_rawData != value)
                {
                    _rawData = value;
                    PropertyChanged?.Invoke(this,
                        new PropertyChangedEventArgs(nameof(RawData)));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
