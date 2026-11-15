using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotationCollectable : MonoBehaviour
{
    public float rotationSpeed = 0.5f;
    [SerializeField] private bool isCheckpoint = false;
    [SerializeField] private int checkpointIndex = 0;
    [SerializeField] private Checkpoints checkpointManager;

    public bool IsCheckpoint => isCheckpoint;
    public int CheckpointIndex => checkpointIndex;


    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        //rotates the object 
        transform.Rotate(0, rotationSpeed, 0 * Time.deltaTime);
    }

    private bool collected = false;

    // Called by the player when this collectable is picked up.
    // Returns false if it was already collected (Destroy only happens at the end of the frame,
    // so the trigger can fire more than once before the object is actually gone).
    public bool Collect()
    {
        if (collected) return false;
        collected = true;

        if (isCheckpoint && checkpointManager != null)
        {
            // Set the checkpoint index in the GameManager
            checkpointManager.ActivateCheckpoint(checkpointIndex);
        }
        return true;
    }

}
