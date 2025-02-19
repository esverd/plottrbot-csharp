using System;
using System.ComponentModel;
using System.Windows.Input;
using Stateless;

namespace plottrBot.ViewModels
{
    public enum GUIState
    {
        Blank,
        BmpLoaded,
        BmpSliced,
        UsbConnected,
        BmpLoadedUsbConnected,
        BmpSlicedUsbConnected,
        BmpDrawing,
        SvgLoaded,
        SvgLoadedUsbConnected,
        SvgDrawing
    }

    public enum GUIAction
    {
        OpenBmp,
        SliceBmp,
        Clear,
        OpenSvg,
        ConnectUsb,
        StartDrawing,
        DisconnectUsb
    }

    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private StateMachine<GUIState, GUIAction> _machine;
        private GUIState _currentState;

        public GUIState CurrentState
        {
            get { return _currentState; }
            private set
            {
                if (_currentState != value)
                {
                    _currentState = value;
                    OnPropertyChanged(nameof(CurrentState));
                    UpdateUIProperties();
                }
            }
        }

        // UI-bound properties
        private bool _isImageLoaded;
        public bool IsImageLoaded
        {
            get => _isImageLoaded;
            private set { _isImageLoaded = value; OnPropertyChanged(nameof(IsImageLoaded)); }
        }

        private bool _isImageSliced;
        public bool IsImageSliced
        {
            get => _isImageSliced;
            private set { _isImageSliced = value; OnPropertyChanged(nameof(IsImageSliced)); }
        }

        private bool _isUsbConnected;
        public bool IsUsbConnected
        {
            get => _isUsbConnected;
            private set { _isUsbConnected = value; OnPropertyChanged(nameof(IsUsbConnected)); }
        }

        private bool _isDrawing;
        public bool IsDrawing
        {
            get => _isDrawing;
            private set { _isDrawing = value; OnPropertyChanged(nameof(IsDrawing)); }
        }

        private bool _isSvgLoaded;
        public bool IsSvgLoaded
        {
            get => _isSvgLoaded;
            private set { _isSvgLoaded = value; OnPropertyChanged(nameof(IsSvgLoaded)); }
        }

        public MainWindowViewModel()
        {
            // Initialize state machine with the initial state
            _machine = new StateMachine<GUIState, GUIAction>(GUIState.Blank);
            ConfigureStateMachine();
            CurrentState = _machine.State;
        }

        private void ConfigureStateMachine()
        {
            // From Blank
            _machine.Configure(GUIState.Blank)
                .Permit(GUIAction.OpenBmp, GUIState.BmpLoaded)
                .Permit(GUIAction.OpenSvg, GUIState.SvgLoaded)
                .Permit(GUIAction.ConnectUsb, GUIState.UsbConnected);

            // From BmpLoaded
            _machine.Configure(GUIState.BmpLoaded)
                .PermitReentry(GUIAction.OpenBmp)  // Self-transition: reenter same state
                .Permit(GUIAction.SliceBmp, GUIState.BmpSliced)
                .Permit(GUIAction.Clear, GUIState.Blank)
                .Permit(GUIAction.OpenSvg, GUIState.SvgLoaded)
                .Permit(GUIAction.ConnectUsb, GUIState.BmpLoadedUsbConnected);

            // From BmpSliced
            _machine.Configure(GUIState.BmpSliced)
                .Permit(GUIAction.OpenBmp, GUIState.BmpLoaded)
                .PermitReentry(GUIAction.SliceBmp)  // Self-transition: reenter same state
                .Permit(GUIAction.Clear, GUIState.Blank)
                .Permit(GUIAction.OpenSvg, GUIState.SvgLoaded)
                .Permit(GUIAction.ConnectUsb, GUIState.BmpSlicedUsbConnected);

            // From UsbConnected
            _machine.Configure(GUIState.UsbConnected)
                .Permit(GUIAction.OpenBmp, GUIState.BmpLoadedUsbConnected)
                .PermitReentry(GUIAction.Clear)         // Self-transition reentry for Clear
                .Permit(GUIAction.OpenSvg, GUIState.SvgLoadedUsbConnected)
                .PermitReentry(GUIAction.ConnectUsb)      // Self-transition reentry for ConnectUsb
                .Permit(GUIAction.DisconnectUsb, GUIState.Blank);

            // From BmpLoadedUsbConnected
            _machine.Configure(GUIState.BmpLoadedUsbConnected)
                .PermitReentry(GUIAction.OpenBmp)         // Self-transition reentry
                .Permit(GUIAction.SliceBmp, GUIState.BmpSlicedUsbConnected)
                .Permit(GUIAction.Clear, GUIState.UsbConnected)
                .Permit(GUIAction.OpenSvg, GUIState.SvgLoadedUsbConnected)
                .PermitReentry(GUIAction.ConnectUsb)
                .Permit(GUIAction.DisconnectUsb, GUIState.BmpLoaded);

            // From BmpSlicedUsbConnected
            _machine.Configure(GUIState.BmpSlicedUsbConnected)
                .Permit(GUIAction.OpenBmp, GUIState.BmpLoadedUsbConnected)
                .PermitReentry(GUIAction.SliceBmp)
                .Permit(GUIAction.Clear, GUIState.UsbConnected)
                .Permit(GUIAction.OpenSvg, GUIState.SvgLoadedUsbConnected)
                .PermitReentry(GUIAction.ConnectUsb)
                .Permit(GUIAction.StartDrawing, GUIState.BmpDrawing)
                .Permit(GUIAction.DisconnectUsb, GUIState.BmpSliced);

            // From BmpDrawing
            _machine.Configure(GUIState.BmpDrawing)
                .Permit(GUIAction.OpenBmp, GUIState.BmpLoadedUsbConnected)
                .Permit(GUIAction.Clear, GUIState.UsbConnected)
                .Permit(GUIAction.OpenSvg, GUIState.SvgLoadedUsbConnected)
                .Permit(GUIAction.StartDrawing, GUIState.BmpSlicedUsbConnected)
                .Permit(GUIAction.DisconnectUsb, GUIState.BmpLoadedUsbConnected);

            // From SvgLoaded
            _machine.Configure(GUIState.SvgLoaded)
                .Permit(GUIAction.OpenBmp, GUIState.BmpLoaded)
                .Permit(GUIAction.Clear, GUIState.Blank)
                .PermitReentry(GUIAction.OpenSvg)         // Self-transition reentry for OpenSvg
                .Permit(GUIAction.ConnectUsb, GUIState.SvgLoadedUsbConnected);

            // From SvgLoadedUsbConnected
            _machine.Configure(GUIState.SvgLoadedUsbConnected)
                .Permit(GUIAction.OpenBmp, GUIState.BmpLoadedUsbConnected)
                .Permit(GUIAction.Clear, GUIState.UsbConnected)
                .PermitReentry(GUIAction.OpenSvg)
                .PermitReentry(GUIAction.ConnectUsb)
                .Permit(GUIAction.StartDrawing, GUIState.SvgDrawing)
                .Permit(GUIAction.DisconnectUsb, GUIState.SvgLoaded);

            // From SvgDrawing
            _machine.Configure(GUIState.SvgDrawing)
                .Permit(GUIAction.OpenBmp, GUIState.BmpLoadedUsbConnected)
                .Permit(GUIAction.Clear, GUIState.UsbConnected)
                .PermitReentry(GUIAction.OpenSvg)
                .Permit(GUIAction.StartDrawing, GUIState.SvgLoadedUsbConnected)
                .Permit(GUIAction.DisconnectUsb, GUIState.SvgLoadedUsbConnected);
        }


        // Method to fire a transition trigger
        public void Fire(GUIAction action)
        {
            _machine.Fire(action);
            CurrentState = _machine.State;
        }

        // Update UI boolean properties based on the current state.
        private void UpdateUIProperties()
        {
            // IsImageLoaded if any BMP is loaded or sliced (with or without USB)
            IsImageLoaded = (CurrentState == GUIState.BmpLoaded ||
                             CurrentState == GUIState.BmpSliced ||
                             CurrentState == GUIState.BmpLoadedUsbConnected ||
                             CurrentState == GUIState.BmpSlicedUsbConnected);

            // IsImageSliced if the state indicates slicing
            IsImageSliced = (CurrentState == GUIState.BmpSliced ||
                             CurrentState == GUIState.BmpSlicedUsbConnected);

            // IsUsbConnected if any USB-connected state is active
            IsUsbConnected = (CurrentState == GUIState.UsbConnected ||
                              CurrentState == GUIState.BmpLoadedUsbConnected ||
                              CurrentState == GUIState.BmpSlicedUsbConnected ||
                              CurrentState == GUIState.SvgLoadedUsbConnected);

            // IsDrawing if currently drawing
            IsDrawing = (CurrentState == GUIState.BmpDrawing ||
                         CurrentState == GUIState.SvgDrawing);

            // IsSvgLoaded if an SVG is loaded (with or without USB or drawing)
            IsSvgLoaded = (CurrentState == GUIState.SvgLoaded ||
                           CurrentState == GUIState.SvgLoadedUsbConnected ||
                           CurrentState == GUIState.SvgDrawing);

            // Notify that these properties have changed.
            OnPropertyChanged(nameof(IsImageLoaded));
            OnPropertyChanged(nameof(IsImageSliced));
            OnPropertyChanged(nameof(IsUsbConnected));
            OnPropertyChanged(nameof(IsDrawing));
            OnPropertyChanged(nameof(IsSvgLoaded));
        }

        #region Commands

        public ICommand OpenBmpCommand => new RelayCommand(() => Fire(GUIAction.OpenBmp));
        public ICommand SliceBmpCommand => new RelayCommand(() => Fire(GUIAction.SliceBmp));
        public ICommand ClearCommand => new RelayCommand(() => Fire(GUIAction.Clear));
        public ICommand OpenSvgCommand => new RelayCommand(() => Fire(GUIAction.OpenSvg));
        public ICommand ConnectUsbCommand => new RelayCommand(() => Fire(GUIAction.ConnectUsb));
        public ICommand StartDrawingCommand => new RelayCommand(() => Fire(GUIAction.StartDrawing));
        public ICommand DisconnectUsbCommand => new RelayCommand(() => Fire(GUIAction.DisconnectUsb));

        #endregion

        #region INotifyPropertyChanged Members
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }

    // A simple RelayCommand implementation
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;
        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException("execute");
            _canExecute = canExecute;
        }
        public bool CanExecute(object parameter) => _canExecute == null || _canExecute();
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
        public void Execute(object parameter) => _execute();
    }
}
