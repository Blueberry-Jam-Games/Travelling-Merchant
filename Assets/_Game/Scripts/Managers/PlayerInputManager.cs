using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputManager : MonoBehaviour
{
    private InputAction interactAction;
    private InputAction lookAction;
    private InputAction moveAction;

    private InputAction rotateLeftAction;
    private InputAction rotateRightAction;

    private EntityManager entityManager;
    private EntityQuery inputQuery;
    private bool interactFlag = false;
    private bool rotateLeftFlag = false;
    private bool rotateRightFlag = false;

    private void OnEnable()
    {
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        inputQuery = entityManager.CreateEntityQuery(typeof(UserInput));

        var actions = InputSystem.actions;
        if (actions == null) { Debug.LogError("InputSystem.actions is null"); return; }

        interactAction = actions.FindAction("Player/Interact", true);
        lookAction = actions.FindAction("Player/Look", true);
        moveAction = actions.FindAction("Player/Move", true);

        rotateLeftAction = actions.FindAction("Player/RotateLeft", true);
        rotateRightAction = actions.FindAction("Player/RotateRight", true);

        Debug.Log($"Interact found={interactAction != null} enabled={interactAction?.enabled}");

        if (interactAction != null) interactAction.performed += OnInteraction;
        if (rotateLeftAction != null) rotateLeftAction.performed += OnRotateLeft;
        if (rotateRightAction != null) rotateRightAction.performed += OnRotateRight;

        // Make sure actions are enabled (safe even if asset is globally enabled)
        interactAction?.Enable();
    }
    // Update is called once per frame
    void Update()
    {
        UserInput userInput = inputQuery.GetSingleton<UserInput>();

        userInput.mouseDelta = lookAction.ReadValue<Vector2>();
        userInput.moveDirection = moveAction.ReadValue<Vector2>();

        userInput.interact = interactFlag;
        userInput.rotateLeft = rotateLeftFlag;
        userInput.rotateRight = rotateRightFlag;
        interactFlag = false;
        rotateLeftFlag = false;
        rotateRightFlag = false;

        inputQuery.SetSingleton(userInput);
    }

    private void OnInteraction(InputAction.CallbackContext ctx)
    {
        interactFlag = true;
    }

    private void OnRotateRight(InputAction.CallbackContext ctx)
    {
        rotateRightFlag = true;
    }

    private void OnRotateLeft(InputAction.CallbackContext ctx)
    {
        rotateLeftFlag = true;
    }
}
