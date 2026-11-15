using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class catDeath : MonoBehaviour
{
    private Animator anim;
    // Start is called before the first frame update
    void Start()
    {
        anim = GetComponent<Animator>();
    }
    //genbrugt code fra goomba script.
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if(collision.gameObject.tag == "Bullet" && !anim.GetBool("dead"))
        {
            Stats.Instance?.RecordEnemyKilled("Shooter", transform.position);
            anim.SetBool("dead", true);
            Destroy(gameObject, 1f);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
