using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapDisplay : MonoBehaviour
{
    public Renderer textureRend;
    public void DrawTexture(Texture2D texture)
    {
        textureRend.sharedMaterial.mainTexture = texture;
        textureRend.transform.localScale = new Vector3(texture.width, 1, texture.height);
    }
}
