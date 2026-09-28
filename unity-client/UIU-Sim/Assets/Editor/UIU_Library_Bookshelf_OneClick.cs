using UnityEngine;
using UnityEditor;

public class UIU_Library_Bookshelf_OneClick
{
    [MenuItem("Tools/Create UIU Library Bookshelf")]
    public static void Create()
    {
        GameObject root = new GameObject("UIU_Library_Bookshelf");
        Material wood = Mat(new Color(0.55f,0.35f,0.18f));
        Material darkWood = Mat(new Color(0.32f,0.18f,0.08f));

        CreateCube("Left Wooden Side", new Vector3(-2.6f,2.2f,0), new Vector3(.18f,4.4f,.55f), root.transform, wood);
        CreateCube("Right Wooden Side", new Vector3(2.6f,2.2f,0), new Vector3(.18f,4.4f,.55f), root.transform, wood);
        CreateCube("Top", new Vector3(0,4.35f,0), new Vector3(5.35f,.18f,.55f), root.transform, wood);
        CreateCube("Bottom", new Vector3(0,.12f,0), new Vector3(5.35f,.18f,.55f), root.transform, wood);

        float[] rows={.45f,1.25f,2.05f,2.85f,3.65f};
        foreach(float y in rows)
        {
            CreateCube("Shelf Board", new Vector3(0,y,0), new Vector3(5.1f,.08f,.55f), root.transform,darkWood);
            float x=-2.35f;
            while(x<2.35f)
            {
                float w=Random.Range(.10f,.18f);
                float h=Random.Range(.45f,.7f);
                GameObject b=CreateCube("Book", new Vector3(x+w/2,y+h/2+.05f,Random.Range(-.03f,.03f)), new Vector3(w,h,.35f), root.transform, Mat(Random.ColorHSV()));
                b.transform.localRotation=Quaternion.Euler(0,0,Random.Range(-4,4));
                x+=w+Random.Range(.015f,.04f);
            }
        }

        PrefabUtility.SaveAsPrefabAsset(root,"Assets/UIU_Library_Bookshelf.prefab");
        Object.DestroyImmediate(root);
        AssetDatabase.Refresh();
    }

    static Material Mat(Color c)
    {
        Shader s=Shader.Find("Universal Render Pipeline/Lit");
        if(s==null) s=Shader.Find("Standard");
        Material m=new Material(s);
        if(m.HasProperty("_BaseColor")) m.SetColor("_BaseColor",c); else m.color=c;
        return m;
    }

    static GameObject CreateCube(string n,Vector3 p,Vector3 s,Transform parent,Material mat)
    {
        GameObject o=GameObject.CreatePrimitive(PrimitiveType.Cube);
        o.name=n;
        o.transform.SetParent(parent);
        o.transform.localPosition=p;
        o.transform.localScale=s;
        o.GetComponent<Renderer>().sharedMaterial=mat;
        return o;
    }
}
