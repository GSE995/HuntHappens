using UnityEngine;
using UnityEngine.Rendering;

namespace HuntHappens.Player
{
    // Presentation only: CharacterController and the existing FPS scripts own all movement.
    [DisallowMultipleComponent]
    public sealed class PlayerCharacterVisual : MonoBehaviour
    {
        [SerializeField] private CharacterController motor;
        [SerializeField] private Camera firstPersonCamera;
        [SerializeField] private Animator bodyAnimator;
        [SerializeField] private SkinnedMeshRenderer fullBody;
        [SerializeField, Min(.1f)] private float referenceWalkSpeed = 1.4f;
        private static readonly int Speed = Animator.StringToHash("Speed");
        private Renderer[] cameraVisuals;
        private bool[] originalForcedVisibility;
        public Animator BodyAnimator => bodyAnimator;
        public SkinnedMeshRenderer FullBody => fullBody;
        public float MovementBlend { get; private set; }
        public void Configure(CharacterController controller, Camera camera, Animator animator,
            SkinnedMeshRenderer body)
        { motor=controller; firstPersonCamera=camera; bodyAnimator=animator; fullBody=body; }
        private void Awake()
        {
            if (bodyAnimator) { bodyAnimator.applyRootMotion=false; bodyAnimator.cullingMode=AnimatorCullingMode.AlwaysAnimate; }
            cameraVisuals=firstPersonCamera ? firstPersonCamera.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            originalForcedVisibility=new bool[cameraVisuals.Length];
            for(int i=0;i<cameraVisuals.Length;i++)originalForcedVisibility[i]=cameraVisuals[i].forceRenderingOff;
        }
        private void OnEnable()
        { RenderPipelineManager.beginCameraRendering+=BeginCamera; RenderPipelineManager.endCameraRendering+=EndCamera; }
        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering-=BeginCamera;RenderPipelineManager.endCameraRendering-=EndCamera;
            Restore();
        }
        private void LateUpdate()
        {
            if (!motor || !bodyAnimator) return;
            Vector3 velocity=motor.velocity;velocity.y=0;
            float blend=motor.isGrounded ? Mathf.Clamp01(velocity.magnitude/referenceWalkSpeed) : 0;
            MovementBlend=Mathf.Lerp(MovementBlend,blend,1-Mathf.Exp(-12*Time.deltaTime));
            bodyAnimator.SetFloat(Speed,MovementBlend);
        }
        private void BeginCamera(ScriptableRenderContext context,Camera camera)
        {
            bool fps=camera==firstPersonCamera;
            if(fullBody)fullBody.shadowCastingMode=fps ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            for(int i=0;i<cameraVisuals.Length;i++)if(cameraVisuals[i])
                cameraVisuals[i].forceRenderingOff=fps ? originalForcedVisibility[i] : true;
        }
        private void EndCamera(ScriptableRenderContext context,Camera camera) => Restore();
        private void Restore()
        {
            if(fullBody)fullBody.shadowCastingMode=ShadowCastingMode.On;
            if(cameraVisuals!=null)for(int i=0;i<cameraVisuals.Length;i++)if(cameraVisuals[i])cameraVisuals[i].forceRenderingOff=originalForcedVisibility[i];
        }
    }
}
