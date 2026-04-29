using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionAsset _inputActions;

    [Header("Camera")]
    [SerializeField] private float _mouseSensitivity = 2f;
    [SerializeField] private float _maxVerticalAngle = 80f;
    [SerializeField] private float _mouseMinDelta = 0.1f;

    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 5f;

    [Header("Raycast")]
    [SerializeField] private float _raycastDistance = 10f;
    [SerializeField] private LayerMask _targetLayerMask = -1;

    [Header("UI")]
    [SerializeField] private GameObject _gameCompletedUI;

    private Camera _playerCamera;
    private CharacterController _characterController;
    private float _verticalRotation;
    private Collider _currentHitCollider;

    private InputActionMap _playerMap;
    private InputAction _moveAction;
    private InputAction _lookAction;
    private InputAction _attackAction;
    private InputAction _exit;

    private bool _wasAttackPressed;
    private bool _controlsEnabled = true;

    private void Awake()
    {
        _playerCamera = Camera.main;
        _characterController = GetComponent<CharacterController>();

        if (_inputActions == null)
        {
            Debug.LogError("InputActionAsset not assigned!");
            return;
        }
        _playerMap = _inputActions.FindActionMap("Player");
        if (_playerMap == null)
        {
            Debug.LogError("ActionMap 'Player' not found!");
            return;
        }
        _moveAction = _playerMap.FindAction("Move");
        _lookAction = _playerMap.FindAction("Look");
        _attackAction = _playerMap.FindAction("Attack");
        _exit = _playerMap.FindAction("Menu");

        if (_moveAction == null || _lookAction == null || _attackAction == null)
        {
            Debug.LogError("Actions Move, Look or Attack not found!");
            return;
        }
        _playerMap.Enable();
    }

    private void Start()
    {
        _verticalRotation = 0f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (_gameCompletedUI != null) _gameCompletedUI.SetActive(false);

        if (ScenarioManager.Instance != null)
        {
            ScenarioManager.Instance.OnAllScenariosCompleted += OnGameCompleted;
            ScenarioManager.Instance.OnRestarted += OnRestarted;
        }
    }

    private void OnRestarted()
    {
        _gameCompletedUI.gameObject.SetActive(false);
        _controlsEnabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDestroy()
    {
        if (ScenarioManager.Instance != null)
            ScenarioManager.Instance.OnAllScenariosCompleted -= OnGameCompleted;
    }

    private void OnGameCompleted()
    {
        _controlsEnabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (_gameCompletedUI != null) _gameCompletedUI.SetActive(true);
    }

    public void RestartGame()
    {
        if (ScenarioManager.Instance != null) ScenarioManager.Instance.ResetGame();

        _controlsEnabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (_gameCompletedUI != null) _gameCompletedUI.SetActive(false);
    }

    private void Update()
    {
        if (!_controlsEnabled) return;

        Vector2 lookInput = _lookAction.ReadValue<Vector2>();
        HandleLook(lookInput);

        Vector2 moveInput = _moveAction.ReadValue<Vector2>();
        HandleMovement(moveInput);

        HandleHighlight();

        bool isHolding = _attackAction.ReadValue<float>() > 0;
        bool justPressed = isHolding && !_wasAttackPressed;
        _wasAttackPressed = isHolding;

        if (_currentHitCollider != null)
            ScenarioManager.Instance?.HoldTarget(_currentHitCollider, isHolding, justPressed, Time.deltaTime);

        if (_exit.IsPressed())
        {
            Debug.Log("Exit");
            Application.Quit();
        }
    }

    private void HandleLook(Vector2 lookInput)
    {
        if (Mathf.Abs(lookInput.x) < _mouseMinDelta) lookInput.x = 0f;
        if (Mathf.Abs(lookInput.y) < _mouseMinDelta) lookInput.y = 0f;

        transform.Rotate(Vector3.up, lookInput.x * _mouseSensitivity);
        _verticalRotation -= lookInput.y * _mouseSensitivity;
        _verticalRotation = Mathf.Clamp(_verticalRotation, -_maxVerticalAngle, _maxVerticalAngle);
        _playerCamera.transform.localRotation = Quaternion.Euler(_verticalRotation, 0f, 0f);
    }

    private void HandleMovement(Vector2 moveInput)
    {
        Vector3 direction = new Vector3(moveInput.x, 0, moveInput.y);
        if (direction.magnitude < 0.1f) return;
        direction = transform.TransformDirection(direction);
        _characterController.SimpleMove(direction * _moveSpeed);
    }

    private void HandleHighlight()
    {
        Ray ray = _playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
        if (Physics.Raycast(ray, out RaycastHit hit, _raycastDistance, _targetLayerMask))
        {
            if (ScenarioManager.Instance != null && ScenarioManager.Instance.IsColliderInCurrentScenarioAndActive(hit.collider))
            {
                if (_currentHitCollider != hit.collider)
                {
                    _currentHitCollider = hit.collider;
                    ScenarioManager.Instance.SetHighlight(_currentHitCollider.gameObject, Color.green, true);
                }
                return;
            }
        }

        if (_currentHitCollider != null)
        {
            _currentHitCollider = null;
            ScenarioManager.Instance?.SetHighlight(null, Color.clear, false);
        }
    }
}