using UnityEngine;

namespace TLA.Gameplay.Interaction
{
    /// <summary>
    /// Test-only interactable for sandbox scenes: brightens while looked at, flashes and logs a
    /// message when used. Shows how a subclass can use the focus callbacks (later, evidence
    /// objects will use the same hooks for a subtle highlight).
    /// </summary>
    public sealed class TestInteractable : Interactable
    {
        [SerializeField] private string message = "You inspected the test object.";
        [SerializeField] private Color focusTint = new Color(1f, 0.85f, 0.4f);
        [SerializeField] private Color flashTint = new Color(0.4f, 1f, 0.5f);

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP shaders

        private Renderer _renderer;
        private MaterialPropertyBlock _block;
        private Color _baseColor = Color.white;
        private float _flashTimer;
        private bool _focused;

        /// <summary>How many times this object has been used (handy for automated tests).</summary>
        public int InteractionCount { get; private set; }

        private void Awake()
        {
            _renderer = GetComponentInChildren<Renderer>();
            _block = new MaterialPropertyBlock();
            if (_renderer != null && _renderer.sharedMaterial != null)
            {
                Material material = _renderer.sharedMaterial;
                if (material.HasProperty(BaseColorId)) _baseColor = material.GetColor(BaseColorId);
                else if (material.HasProperty(ColorId)) _baseColor = material.GetColor(ColorId);
            }
        }

        private void Update()
        {
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                ApplyColor();
            }
        }

        protected override void OnInteract(PlayerInteractor interactor)
        {
            InteractionCount++;
            Debug.Log($"[TestInteractable] {name}: {message} (x{InteractionCount})", this);
            _flashTimer = 0.25f;
            ApplyColor();
        }

        public override void OnFocusGained()
        {
            _focused = true;
            ApplyColor();
        }

        public override void OnFocusLost()
        {
            _focused = false;
            ApplyColor();
        }

        // MaterialPropertyBlock changes the colour of THIS object only, without creating a material copy.
        private void ApplyColor()
        {
            if (_renderer == null) return;

            Color color = _flashTimer > 0f ? flashTint : _focused ? _baseColor * focusTint : _baseColor;
            _renderer.GetPropertyBlock(_block);
            _block.SetColor(ColorId, color);
            _block.SetColor(BaseColorId, color);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
