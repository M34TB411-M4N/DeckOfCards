using UnityEngine;
using TMPro;

public class ScoreEntry : MonoBehaviour {
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI scoreText;

    public void SetData(string name, int score) {
        nameText.text = name;
        scoreText.text = score.ToString();
    }
}