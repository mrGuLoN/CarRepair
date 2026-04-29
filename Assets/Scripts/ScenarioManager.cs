using System.Collections.Generic;
using TMPro;
using UnityEngine;

[System.Serializable]
public class ScenarioData
{
    public Transform[] target;
    public float holdTimes;
    public string scenarioName;
}

public class ScenarioManager : MonoBehaviour
{
    private static readonly int Work = Animator.StringToHash("Work");
    public static ScenarioManager Instance { get; private set; }

    public System.Action OnAllScenariosCompleted;
    public System.Action OnRestarted;

    [SerializeField] private ScenarioData[] scenarioDatas;
    [SerializeField] private TMP_Text _scenarioText;
    [SerializeField] private Outline _outline; 

    private List<Collider>[] scenarioColliders;    
    private float[] holdTimesForScenario;           
    private Dictionary<Collider, float> progress;  
    private Dictionary<Collider, bool> animStarted;  
    private int currentScenario = -1;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        int count = scenarioDatas.Length;
        scenarioColliders = new List<Collider>[count];
        holdTimesForScenario = new float[count];
        progress = new Dictionary<Collider, float>();
        animStarted = new Dictionary<Collider, bool>();

        for (int i = 0; i < count; i++)
        {
            var data = scenarioDatas[i];
            List<Collider> list = new List<Collider>();
            Debug.Log(data.target + " / " + i);
            for (int j = 0; j < data.target.Length; j++)
            {
                Collider col = data.target[j].GetComponent<Collider>();
                if (col != null) list.Add(col);
            }
            scenarioColliders[i] = list;
            holdTimesForScenario[i] = data.holdTimes;
        }

        for (int i = 0; i < count; i++)
            foreach (var col in scenarioColliders[i])
                if (col != null) col.enabled = false;

        SwitchToScenario(0);
    }

    private void SwitchToScenario(int index)
    {
        if (currentScenario >= 0)
        {
            foreach (var col in scenarioColliders[currentScenario])
                if (col != null) col.enabled = false;
        }

        currentScenario = index;
        _scenarioText.text = scenarioDatas[index].scenarioName;
        foreach (var col in scenarioColliders[currentScenario])
            if (col != null) col.enabled = true;

        progress.Clear();
        animStarted.Clear();
        foreach (var col in scenarioColliders[currentScenario])
        {
            progress[col] = 0f;
            animStarted[col] = false;
        }
    }

    public void HoldTarget(Collider col, bool isHolding, bool justPressed, float deltaTime)
    {
        if (col == null) return;
        if (!col.enabled) return;           

        bool belongs = false;
        foreach (var c in scenarioColliders[currentScenario])
            if (c == col) { belongs = true; break; }
        if (!belongs) return;

        float required = holdTimesForScenario[currentScenario];
        if (required <= 0f)  
        {
            if (justPressed)
            {
                col.enabled = false;
                PlayAnim(col);
                CheckComplete();
            }
            return;
        }

        if (progress[col] >= required)
        {
            col.enabled = false;
            progress.Remove(col);
            animStarted.Remove(col);
            CheckComplete();
            return;
        }

        if (!animStarted[col] && justPressed)
        {
            PlayAnim(col);
            animStarted[col] = true;
            SetAnimSpeed(col, 0f);
        }
    
        if (isHolding)
        {
            SetAnimSpeed(col, 1f);
            progress[col] += deltaTime;

            if (progress[col] >= required)
            {
                col.enabled = false;
                progress.Remove(col);
                animStarted.Remove(col);
                CheckComplete();
            }
        }
        else
        {
            SetAnimSpeed(col, 0f);
        }
    }

    private void PlayAnim(Collider col)
    {
        int idx = System.Array.IndexOf(scenarioColliders[currentScenario].ToArray(), col);
        if (idx >= 0)
        {
            Animator anim = scenarioDatas[currentScenario].target[idx].GetComponent<Animator>();
            if (anim != null) anim.SetTrigger(Work);
        }
    }

    private void SetAnimSpeed(Collider col, float speed)
    {
        int idx = System.Array.IndexOf(scenarioColliders[currentScenario].ToArray(), col);
        if (idx >= 0)
        {
            Animator anim = scenarioDatas[currentScenario].target[idx].GetComponent<Animator>();
            if (anim != null) anim.speed = speed;
        }
    }

    private void CheckComplete()
    {
        foreach (var col in scenarioColliders[currentScenario])
            if (col != null && col.enabled) return;

        int next = currentScenario + 1;
        if (next < scenarioDatas.Length)
        {
            SwitchToScenario(next);
        }
        else
        {
            Debug.Log("Все сценарии пройдены");
            _scenarioText.text = "Вы сменили тормозной диск";
            OnAllScenariosCompleted?.Invoke();
        }
    }

    public bool IsColliderInCurrentScenarioAndActive(Collider col)
    {
        if (col == null) return false;
        if (!col.enabled) return false;
        foreach (var c in scenarioColliders[currentScenario])
            if (c == col) return true;
        return false;
    }

  
    public void ResetGame()
    {
        for (int i = 0; i < scenarioColliders.Length; i++)
            foreach (var col in scenarioColliders[i])
                if (col != null) col.enabled = false;

        progress.Clear();
        animStarted.Clear();
        OnRestarted?.Invoke();
        currentScenario = -1;
        SwitchToScenario(0);
    }

    public void SetHighlight(GameObject target, Color color, bool enable)
    {
        if (_outline == null) return;
        if (enable && target != null)
        {
            _outline.SetTarget(target);
            _outline.SetColor(color);
        }
        else
        {
            _outline.SetTarget(null);
        }
    }
}