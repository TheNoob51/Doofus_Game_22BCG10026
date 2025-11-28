using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuHandler : MonoBehaviour
{
    public void OnPlay()
    {
        SceneManager.LoadScene("GameScene");
    }
}
