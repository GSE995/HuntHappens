using HuntHappens.Interaction;
using HuntHappens.Player;
using UnityEngine;

namespace HuntHappens.Transport
{
    [DisallowMultipleComponent, RequireComponent(typeof(CharacterController))]
    public sealed class CartDriver : MonoBehaviour
    {
        public DeerCart Cart { get; private set; }
        public bool IsDriving => Cart != null;
        public float MoveSpeed => Cart != null ? Cart.TravelSpeed : 0;
        private CharacterController controller;
        private PrototypeInput input;
        private void Awake() { controller = GetComponent<CharacterController>(); input = GetComponent<PrototypeInput>(); }

        public bool TryTake(DeerCart cart)
        {
            if (IsDriving || cart == null || !cart.isActiveAndEnabled || !cart.IsPurchased || cart.Driver != null ||
                input.GameplayBlocked) return false;
            if (Vector3.Distance(transform.position, cart.HandlePosition) > 1.7f)
            { cart.ShowNotice("Подойдите к ручке тележки"); return false; }
            Vector3 toHandle = cart.HandlePosition - transform.position;
            toHandle.y = 0;
            if (!CanMove(toHandle, cart)) { cart.ShowNotice("Проход к ручке перекрыт"); return false; }
            if (!GetComponent<PlayerInventory>().TryStowForCart())
            { cart.ShowNotice("Освободите руки — в инвентаре нет места"); return false; }
            controller.Move(toHandle);
            if (Vector3.ProjectOnPlane(transform.position - cart.HandlePosition, Vector3.up).magnitude > .15f) return false;
            Cart = cart;
            cart.SetDriver(this);
            transform.rotation = cart.transform.rotation;
            return true;
        }

        public void Release()
        {
            var cart = Cart;
            Cart = null;
            if (cart != null) cart.SetDriver(null);
        }

        public bool CanMove(Vector3 delta, DeerCart cart)
        {
            if (delta.sqrMagnitude < .000001f) return true;
            Vector3 center = transform.TransformPoint(controller.center);
            float radius = controller.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
            float half = Mathf.Max(0, controller.height * transform.lossyScale.y * .5f - radius);
            foreach (var hit in Physics.CapsuleCastAll(center + Vector3.up * half, center - Vector3.up * half,
                         radius * .95f, delta.normalized, delta.magnitude + .02f, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(transform) && !cart.Owns(hit.collider) && hit.normal.y < .7f)
                    return false;
            return true;
        }

        // Called by the existing player motor after it has read input and rotated the view.
        public CollisionFlags Move(Vector3 displacement)
        {
            if (Cart == null) return controller.Move(displacement);
            if (input.GameplayBlocked || Cursor.lockState != CursorLockMode.Locked ||
                Vector3.Distance(transform.position, Cart.HandlePosition) > 4)
            { Release(); return controller.Move(Vector3.up * displacement.y); }
            var cart = Cart;
            Quaternion heading = Quaternion.RotateTowards(cart.transform.rotation,
                Quaternion.Euler(0, transform.eulerAngles.y, 0), 80 * Time.deltaTime);
            cart.TryMove(Vector3.ProjectOnPlane(displacement, Vector3.up), heading);
            Vector3 target = cart.HandlePosition;
            Vector3 delta = target - transform.position;
            delta.y = displacement.y;
            var result = controller.Move(delta);
            if (Vector3.ProjectOnPlane(transform.position - target, Vector3.up).magnitude > .5f) Release();
            return result;
        }

        private void OnDisable() => Release();
    }
}
