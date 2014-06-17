using SharpDX.Toolkit;

namespace VirtualSpace.Platform.Windows.Rendering
{
    public class SubGameSystem : GameSystem
    {
        private readonly GameSystem _parent;
        private bool _isEnabled;
        private bool _isVisible;

        private bool _isDisposed;
        private bool _hasLoaded;

        public SubGameSystem(GameSystem parent)
            : base(parent.Game)
        {
            _parent = parent;

            _parent.EnabledChanged += SubEnabledChanged;
            _parent.VisibleChanged += SubVisibleChanged;

            base.Enabled = false;
            base.Visible = false;

            parent.Game.GameSystems.Add(this);
        }

        private void SubVisibleChanged(object sender, System.EventArgs e)
        {
            base.Visible = _parent.Visible && _isVisible;
        }

        private void SubEnabledChanged(object sender, System.EventArgs e)
        {
            base.Enabled = _parent.Enabled && _isEnabled;
        }

        public new bool Enabled
        {
            get
            {
                return _parent.Enabled && _isEnabled;
            }
            set
            {
                _isEnabled = value;
                base.Enabled = _parent.Enabled && _isEnabled;
            }
        }

        public new bool Visible
        {
            get
            {
                return _parent.Visible && _isVisible;
            }
            set
            {
                _isVisible = value;
                base.Visible = _parent.Visible && _isVisible;
            }
        }

        protected override void LoadContent()
        {
            base.LoadContent();
            _hasLoaded = true;
        }

        protected override void UnloadContent()
        {
            if (_hasLoaded)
            {
                base.UnloadContent();
                _hasLoaded = false;
            }
        }

        protected override void Dispose(bool disposeManagedResources)
        {
            if (!_isDisposed)
            {
                Game.GameSystems.Remove(this);
                (this as IContentable).UnloadContent();

                _parent.EnabledChanged -= SubEnabledChanged;
                _parent.VisibleChanged -= SubVisibleChanged;
                _isDisposed = true;
            }
            
            base.Dispose(disposeManagedResources);
        }
    }
}
