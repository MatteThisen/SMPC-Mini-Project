using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public class Checkpoints : MonoBehaviour
{
    [SerializeField] private List<GameObject> checkpointTilemaps = new List<GameObject>();
    [SerializeField] private List<GameObject> checkpointSpawnpoints = new List<GameObject>();
    public Transform playerSpawnpoint;

    public float timerDuration = 10f; // Duration of the timer in seconds
    public float timerValue; // Current value of the timer
    public bool isTimerActive = false; // Flag to check if the timer is active
    public bool usePitchModulatedMusic = false;

    public TMP_Text timerText; // Reference to the TextMeshProUGUI component for displaying the timer
    public TMP_Text timerValueText; // Reference to the TextMeshProUGUI component for displaying the timer value
    public TMP_Text timerExplainText; // Reference to the TextMeshProUGUI component for displaying the timer explanation

    // Update is called once per frame
    void Update()
    {
        if (isTimerActive)
        {
            UpdateTimer();
        }
    }

    public void ActivateCheckpoint(int checkpointIndex)
    {
        checkpointTilemaps[checkpointIndex].GetComponent<Tilemap>().enabled = true;
        checkpointTilemaps[checkpointIndex].GetComponent<TilemapRenderer>().enabled = true;
        checkpointTilemaps[checkpointIndex].GetComponent<TilemapCollider2D>().enabled = true;
        playerSpawnpoint.position = checkpointSpawnpoints[checkpointIndex].transform.position;

        if (checkpointIndex == 1)
        {
            ActivateTimer();
        }

    }

    public void ActivateTimer()
    {
        timerValue = timerDuration;

        if (usePitchModulatedMusic)
        {
            //PLAY MUSIC HERE WITH PITCH MODULATION
        }
        else
        {
            //PLAY MUSIC HERE WITHOUT PITCH MODULATION
        }

        StartCoroutine(FlashExplainText());
    }

    public void UpdateTimer()
    {
        timerValue -= Time.deltaTime;
        if (timerValue <= 0f)
        {
            timerValue = 0f;
            isTimerActive = false;
            Stats.Instance?.RecordTimerFinished(true, 0f);
            // Timer has finished, perform any necessary actions here
        }

        timerValueText.text = timerValue.ToString("F2"); // Update the timer value text with two decimal places
    }

    IEnumerator FlashExplainText()
    {

        timerText.gameObject.SetActive(true);
        timerValueText.gameObject.SetActive(true);

        for (int i = 0; i < 3; i++)
        {
            timerExplainText.gameObject.SetActive(true);
            yield return new WaitForSeconds(0.5f); // Display for 0.5 seconds
            timerExplainText.gameObject.SetActive(false);
            yield return new WaitForSeconds(0.5f); // Hide for 0.5 seconds
        }

        isTimerActive = true;

    }

}
