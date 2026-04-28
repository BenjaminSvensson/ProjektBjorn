using TMPro;
using UnityEngine;
using System.Collections;

public class Bonus_script : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI str_text;
    [SerializeField] private TextMeshProUGUI spd_text;
    [SerializeField] private TextMeshProUGUI bonus_text;

    public Multipliers multiplier;

    Coroutine bonusRoutine;
    private void Start()
    {
        bonus_text.alpha = 0f;
        bonus_text.text = "";
    }
    void Update()
    {
        str_text.text = multiplier.strength.ToString() + "x";
        spd_text.text = multiplier.speed.ToString() + "x";
    }

    // Call this when picking something up
    public void ShowBonus(string message)
    {
        if (bonusRoutine != null)
            StopCoroutine(bonusRoutine);

        bonusRoutine = StartCoroutine(BonusTextRoutine(message));
    }

    IEnumerator BonusTextRoutine(string message)
    {
        bonus_text.text = message + " increased";
        bonus_text.alpha = 1f;

        yield return new WaitForSeconds(1.5f);

        float t = 0f;
        while (t < 0.5f)
        {
            t += Time.deltaTime;
            bonus_text.alpha = Mathf.Lerp(1f, 0f, t / 0.5f);
            yield return null;
        }

        bonus_text.text = "";
    }
}
