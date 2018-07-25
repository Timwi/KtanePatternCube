using System.Collections.Generic;
using System.Linq;
using PatternCube;
using UnityEngine;
using Rnd = UnityEngine.Random;

/// <summary>
/// On the Subject of Pattern Cube
/// Created by Timwi
/// </summary>
public class PatternCubeModule : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;
    public KMSelectable MainSelectable;

    public Mesh[] NetMeshes;
    public KMSelectable[] SelectableBoxes;
    public KMSelectable[] PlaceableBoxes;
    public MeshFilter ModuleFront;

    private static int _moduleIdCounter = 1;
    private int _moduleId;

    void ArrangeVisuals(Net net)
    {
        // Arrange the placeable boxes
        const float x1 = -.8f;
        const float x2 = .8f;
        const float y1 = -.8f;
        const float y2 = .8f;
        const float w = (x2 - x1) / 5.5f;
        const float h = (y2 - y1) / 5.5f;

        MainSelectable.Children = new KMSelectable[25];
        for (int i = 0; i < 5; i++)
            MainSelectable.Children[i * 5] = SelectableBoxes[i];

        var rc = 0;
        for (int y = 0; y < net.Faces.GetLength(1); y++)
            for (int x = 0; x < net.Faces.GetLength(0); x++)
            {
                if (net.Faces[x, y] == null)
                    continue;
                var box = PlaceableBoxes[rc];
                box.transform.localPosition = new Vector3(x1 + w * (x + 2), 0, y1 + h * (net.Faces.GetLength(1) - y - .5f)) * .1f;
                MainSelectable.Children[(5 - net.Faces.GetLength(1) + y) * 5 + x] = box;
                rc++;
            }

        MainSelectable.UpdateChildren(SelectableBoxes[0]);

        // Set the correct mesh for the module front
        var id = "ModuleFront_" + net.ID;
        ModuleFront.mesh = NetMeshes.First(m => m.name == id);
    }

    void Start()
    {
        _moduleId = _moduleIdCounter++;
        var net = Data.Nets[Rnd.Range(0, Data.Nets.Length)];
        Log("Net size: {0} × {1}", net.Faces.GetLength(0), net.Faces.GetLength(1));
        ArrangeVisuals(net);
    }

    void Log(string msg, params object[] fmtArgs)
    {
        Debug.LogFormat(@"[Pattern Cube #{0}] {1}", _moduleId, string.Format(msg, fmtArgs));
    }
}
