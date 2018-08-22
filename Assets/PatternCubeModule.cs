using System.Collections;
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
    public Texture[] SymbolTextures;
    public Material FrameBlue, FrameGreen, FrameRed, FrameYellow, ScreenDark, ScreenLight;

    private readonly MeshRenderer[] _selectableSymbolObjs = new MeshRenderer[5];
    private readonly MeshRenderer[] _placeableSymbolObjs = new MeshRenderer[6];
    private readonly MeshRenderer[] _selectableFrameObjs = new MeshRenderer[5];
    private readonly MeshRenderer[] _placeableFrameObjs = new MeshRenderer[6];
    private readonly MeshRenderer[] _selectableScreenObjs = new MeshRenderer[5];
    private readonly MeshRenderer[] _placeableScreenObjs = new MeshRenderer[6];

    private static int _moduleIdCounter = 1;
    private int _moduleId;
    private Net _puzzle;
    private FaceSymbol[] _solution;
    private FaceSymbol[] _selectableSymbols;
    private int _faceGivenByHighlight;
    private int _lastSelectableIx = -1;
    private int _highlightedPosition;   // for Souvenir
    private readonly List<int> _facesRevealed = new List<int>();
    private Dictionary<char, Texture> _symbolTextures;

    private sealed class RotateTask { public int From; public int To; }
    private readonly Queue<RotateTask>[] _queues = new Queue<RotateTask>[5];

    void Start()
    {
        _moduleId = _moduleIdCounter++;

        for (int i = 0; i < 5; i++)
        {
            _selectableSymbolObjs[i] = SelectableBoxes[i].transform.Find("Symbol").GetComponent<MeshRenderer>();
            _selectableFrameObjs[i] = SelectableBoxes[i].transform.Find("Frame").GetComponent<MeshRenderer>();
            _selectableScreenObjs[i] = SelectableBoxes[i].transform.Find("Screen").GetComponent<MeshRenderer>();
            _queues[i] = new Queue<RotateTask>();
            StartCoroutine(rotator(i));
        }
        for (int i = 0; i < 6; i++)
        {
            _placeableSymbolObjs[i] = PlaceableBoxes[i].transform.Find("Symbol").GetComponent<MeshRenderer>();
            _placeableFrameObjs[i] = PlaceableBoxes[i].transform.Find("Frame").GetComponent<MeshRenderer>();
            _placeableScreenObjs[i] = PlaceableBoxes[i].transform.Find("Screen").GetComponent<MeshRenderer>();
        }

        _symbolTextures = new Dictionary<char, Texture>();
        foreach (var tx in SymbolTextures)
            _symbolTextures[tx.name[6]] = tx;

        //for (int i = 0; i < 25; i++)
        //    if (i % 5 != 0)
        //        MainSelectable.Children[i] = null;

        // Generate a puzzle
        _puzzle = Data.Nets[Rnd.Range(0, Data.Nets.Length)];
        _faceGivenByHighlight = Rnd.Range(0, 6);
        HalfCube[] arrangement1, arrangement2;
        if (Rnd.Range(0, 2) == 0)
        {
            arrangement1 = Data.Group1;
            arrangement2 = Data.Group2;
        }
        else
        {
            arrangement1 = Data.Group2;
            arrangement2 = Data.Group1;
        }
        var halfCube1 = arrangement1[Rnd.Range(0, arrangement1.Length)];
        var symbolsAlready = new[] { halfCube1.Top.Symbol, halfCube1.Left.Symbol, halfCube1.Front.Symbol };
        var halfCube2Candidates = arrangement2.Where(ag => !new[] { ag.Top.Symbol, ag.Left.Symbol, ag.Front.Symbol }.Intersect(symbolsAlready).Any()).ToArray();
        var halfCube2 = halfCube2Candidates[Rnd.Range(0, halfCube2Candidates.Length)];
        FaceSymbol right, back, bottom;
        switch (Rnd.Range(0, 3))
        {
            case 0:
                back = new FaceSymbol(halfCube2.Front.Symbol, (halfCube2.Front.Orientation + 3) % 4);
                right = new FaceSymbol(halfCube2.Top.Symbol, (halfCube2.Top.Orientation + 3) % 4);
                bottom = new FaceSymbol(halfCube2.Left.Symbol, (halfCube2.Left.Orientation + 3) % 4);
                break;
            case 1:
                back = new FaceSymbol(halfCube2.Top.Symbol, halfCube2.Top.Orientation);
                right = new FaceSymbol(halfCube2.Left.Symbol, (halfCube2.Left.Orientation + 1) % 4);
                bottom = new FaceSymbol(halfCube2.Front.Symbol, halfCube2.Front.Orientation);
                break;
            default:
                back = new FaceSymbol(halfCube2.Left.Symbol, (halfCube2.Left.Orientation + 2) % 4);
                right = new FaceSymbol(halfCube2.Front.Symbol, (halfCube2.Front.Orientation + 2) % 4);
                bottom = new FaceSymbol(halfCube2.Top.Symbol, (halfCube2.Top.Orientation + 1) % 4);
                break;
        }
        var faceGivenFully = (new[] { 0, 1, 4 }.Contains(_faceGivenByHighlight) ? new[] { 2, 3, 5 } : new[] { 0, 1, 4 })[Rnd.Range(0, 3)];
        _facesRevealed.Add(faceGivenFully);
        _solution = new[] { halfCube1.Top, halfCube1.Front, right, back, halfCube1.Left, bottom };
        _selectableSymbols = _solution.Except(new[] { _solution[faceGivenFully] }).ToArray().Shuffle();
        for (int i = 0; i < _selectableSymbols.Length; i++)
        {
            _selectableSymbols[i] = new FaceSymbol(_selectableSymbols[i].Symbol, Rnd.Range(0, 4));
            if (_selectableSymbols[i].Symbol == _solution[_faceGivenByHighlight].Symbol)
                _highlightedPosition = i;
        }

        // Populate the selectable boxes
        for (int i = 0; i < 5; i++)
        {
            _selectableSymbolObjs[i].material.mainTexture = _symbolTextures[_selectableSymbols[i].Symbol];
            _selectableSymbolObjs[i].transform.localEulerAngles = new Vector3(90, _selectableSymbols[i].Orientation * 90, 0);
            _selectableFrameObjs[i].material = FrameRed;
            _selectableScreenObjs[i].material = _selectableSymbols[i].Symbol == _solution[_faceGivenByHighlight].Symbol ? ScreenLight : ScreenDark;
            SelectableBoxes[i].OnInteract = GetSelectableHandler(i);
        }

        // Arrange the placeable boxes
        const float x1 = -.8f;
        const float x2 = .8f;
        const float y1 = -.8f;
        const float y2 = .8f;
        const float w = (x2 - x1) / 5.5f;
        const float h = (y2 - y1) / 5.5f;

        for (int y = 0; y < _puzzle.Faces.GetLength(1); y++)
            for (int x = 0; x < _puzzle.Faces.GetLength(0); x++)
            {
                if (_puzzle.Faces[x, y] == null)
                    continue;
                var fi = _puzzle.Faces[x, y];
                _placeableSymbolObjs[fi.Face].transform.localEulerAngles = new Vector3(90, (fi.Orientation + _solution[fi.Face].Orientation) * 90, 0);
                _placeableSymbolObjs[fi.Face].material.mainTexture = _symbolTextures[_solution[fi.Face].Symbol];
                _placeableSymbolObjs[fi.Face].gameObject.SetActive(fi.Face == faceGivenFully);
                _placeableFrameObjs[fi.Face].material = fi.Face == faceGivenFully ? FrameGreen : FrameYellow;
                _placeableScreenObjs[fi.Face].material = fi.Face == _faceGivenByHighlight ? ScreenLight : ScreenDark;
                PlaceableBoxes[fi.Face].transform.localPosition = new Vector3(x1 + w * (x + 2), 0, y1 + h * (_puzzle.Faces.GetLength(1) - y - .5f)) * .1f;
                PlaceableBoxes[fi.Face].OnInteract = GetPlaceableHandler(fi);
                //MainSelectable.Children[(5 - _puzzle.Faces.GetLength(1) + y) * 5 + x] = PlaceableBoxes[fi.Face];
            }

        //MainSelectable.UpdateChildren(SelectableBoxes[0]);

        // Set the correct mesh for the module front
        var id = "ModuleFront_" + _puzzle.ID;
        ModuleFront.mesh = NetMeshes.First(m => m.name == id);

        Log(@"=svg[Puzzle:]<svg xmlns='http://www.w3.org/2000/svg' viewBox='-3 -3 1206 {0}'>{1}</svg>",
            /* {0} */ 120 * _puzzle.Faces.GetLength(1) + 6,
            /* {1} */ Enumerable.Range(0, _puzzle.Faces.GetLength(1)).SelectMany(y => Enumerable.Range(0, _puzzle.Faces.GetLength(0)).Select(x =>
                _puzzle.Faces[x, y] == null ? null :
                _puzzle.Faces[x, y].Face == faceGivenFully ? svg(x, y, _solution[_puzzle.Faces[x, y].Face].Symbol, (_solution[_puzzle.Faces[x, y].Face].Orientation + _puzzle.Faces[x, y].Orientation) % 4) :
                svg(x, y, highlighted: _puzzle.Faces[x, y].Face == _faceGivenByHighlight))).JoinString());

        Log(@"=svg[Symbols:]<svg xmlns='http://www.w3.org/2000/svg' viewBox='-3 -3 1206 126'>{0}</svg>",
            _selectableSymbols.Select((s, ix) => svgPath(ix, 0, s.Symbol, 0)).JoinString());
        Log(@" The highlighted symbol is {0}.", "first,second,third,4th,5th".Split(',')[_highlightedPosition]);

        Log(@"=svg[Solution:]<svg xmlns='http://www.w3.org/2000/svg' viewBox='-3 -3 1206 {0}'>{1}</svg>",
            /* {0} */ 120 * _puzzle.Faces.GetLength(1) + 6,
            /* {1} */ Enumerable.Range(0, _puzzle.Faces.GetLength(1)).SelectMany(y => Enumerable.Range(0, _puzzle.Faces.GetLength(0)).Select(x =>
                _puzzle.Faces[x, y] == null ? null :
                svg(x, y, _solution[_puzzle.Faces[x, y].Face].Symbol, (_solution[_puzzle.Faces[x, y].Face].Orientation + _puzzle.Faces[x, y].Orientation) % 4))).JoinString());
    }

    private static string svg(int x, int y, char symbol = '\0', int orientation = -1, bool highlighted = false)
    {
        var rect = string.Format("<rect x='{0}' y='{1}' width='120' height='120' fill='{2}' stroke='black' stroke-width='2'/>", x * 120, y * 120, highlighted ? "#f88" : "none");
        if (symbol == '\0')
            return rect;
        return rect + svgPath(x, y, symbol, orientation);
    }
    private static string svgPath(int x, int y, char symbol, int orientation)
    {
        var sy = _svgSymbols[symbol];
        return string.Format("<path d='{5}' transform='translate({3}, {4}) rotate({2}) translate({0}, {1})'/>", -sy.X - 60, -sy.Y - 60, 90 * orientation, 120 * x + 60, 120 * y + 60, sy.SvgPath);
    }

    sealed class SvgSymbol
    {
        public string SvgPath { get; private set; }
        public int X { get; private set; }
        public int Y { get; private set; }
        public SvgSymbol(int x, int y, string svgPath)
        {
            SvgPath = svgPath;
            X = x;
            Y = y;
        }
    }

    private readonly static Dictionary<char, SvgSymbol> _svgSymbols = new Dictionary<char, SvgSymbol> {
        { 'A', new SvgSymbol(0, 0, "M60 10a50 50 0 1 0 0 100 50 50 0 0 0 0-100zm0 10a40 40 0 0 1 40 40H20a40 40 0 0 1 40-40z") },
        { 'B', new SvgSymbol(120, 0, "M180 10l-50 100h100z") },
        { 'C', new SvgSymbol(240, 0, "M330 40a30 30 0 1 1-60 0 30 30 0 1 1 60 0zm-70 40h80v30h-80z") },
        { 'D', new SvgSymbol(360, 0, "M420 10l-5.69 56.81-20.19-53.4 9.22 56.37L370 23.41C385 55 395 80 395 110h50c0-30 10-55 25-86.6l-33.34 46.38 9.21-56.37-20.18 53.4z") },
        { 'E', new SvgSymbol(0, 120, "M20 140v80h80v-80H20zm40 10h30v30H60v-30z") },
        { 'F', new SvgSymbol(120, 120, "M200 130a30 30 0 0 0-27.06 42.94l-40 40a10 10 0 1 0 14.12 14.12l40-40A30 30 0 1 0 200 130z") },
        { 'G', new SvgSymbol(240, 120, "M300 130a20 20 0 1 0 0 40 20 20 0 0 0 0-40zm0 40a30 30 0 1 0 0 60 30 30 0 0 0 0-60zm0 10a20 20 0 1 1 0 40 20 20 0 0 1 0-40z") },
        { 'H', new SvgSymbol(360, 120, "M380 140h80v80h-30v-50h-50z") },
        { 'X', new SvgSymbol(0, 240, "M60 250l-50 50h100zm0 50l-50 50h100z") },
        { 'Y', new SvgSymbol(120, 240, "M220 350v-10h-30l40-40-50-50-50 50 40 40h-30v10h80z") },
        { 'Z', new SvgSymbol(240, 240, "M250 250h100l-40 40v60h-20v-60z") }
    };

    private IEnumerator rotator(int ix)
    {
        const float duration = .15f;

        while (true)
        {
            yield return null;
            if (_queues[ix].Count == 0)
                continue;

            var elem = _queues[ix].Dequeue();
            var elapsed = 0f;
            while (elapsed < duration)
            {
                yield return null;
                elapsed += Time.deltaTime;
                var t = easeOutSine(Mathf.Min(elapsed, duration), duration, 0, 1);
                _selectableSymbolObjs[ix].transform.localRotation = Quaternion.Slerp(Quaternion.Euler(90, elem.From * 90, 0), Quaternion.Euler(90, elem.To * 90, 0), t);
            }
        }
    }

    private string str(FaceInfo fi)
    {
        if (fi == null)
            return "-";
        var fs = _solution[fi.Face];
        return fs.Symbol + "NESW".Substring((fs.Orientation + fi.Orientation) % 4, 1);
    }

    private KMSelectable.OnInteractHandler GetPlaceableHandler(FaceInfo fi)
    {
        return delegate
        {
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, PlaceableBoxes[fi.Face].transform);
            PlaceableBoxes[fi.Face].AddInteractionPunch();

            if (!_selectableSymbols.Any(sel => sel != null && sel.Symbol == _solution[fi.Face].Symbol))
                // The user clicked on a square that is already filled; allow that and do nothing.
                return false;

            if (_lastSelectableIx == -1)
            {
                Log(" You tried to place a symbol without selecting a symbol first. Strike.");
                Module.HandleStrike();
            }
            else if (_selectableSymbols[_lastSelectableIx] == null)
            {
                Log(" You tried to place a symbol that you already placed. Strike.");
                Module.HandleStrike();
            }
            else
            {
                var sym = _selectableSymbols[_lastSelectableIx];
                if (sym.Symbol != _solution[fi.Face].Symbol)
                {
                    Log(" You tried to place symbol {0} where symbol {1} should have gone. Strike.", _solution.IndexOf(fs => fs.Symbol == sym.Symbol) + 1, fi.Face + 1);
                    Module.HandleStrike();
                }
                else if (sym.Orientation != (_solution[fi.Face].Orientation + fi.Orientation) % 4)
                {
                    Log(" You tried to place symbol {0} in the correct place, but wrong orientation ({1} instead of {2}). Strike.", fi.Face + 1, "NESW"[sym.Orientation], "NESW"[(_solution[fi.Face].Orientation + fi.Orientation) % 4]);
                    Module.HandleStrike();
                }
                else
                {
                    // Correct placement
                    Log(" Symbol {0} placed correctly.", fi.Face + 1);
                    _selectableSymbols[_lastSelectableIx] = null;
                    AssignSymbols();
                    StartCoroutine(animatePlacedSymbol(sym.Symbol));
                    if (_selectableSymbols.All(s => s == null))
                    {
                        Log(" Module solved.");
                        Module.HandlePass();
                    }
                }
            }
            return false;
        };
    }

    private static float easeOutSine(float time, float duration, float from, float to)
    {
        return (to - from) * Mathf.Sin(time / duration * (Mathf.PI / 2)) + from;
    }

    private IEnumerator animatePlacedSymbol(char symbol)
    {
        var ix = -1;
        for (int i = 0; i < 6; i++)
            if (_solution[i].Symbol == symbol)
            {
                ix = i;
                break;
            }
        if (ix == -1)
            yield break;

        const float duration = .25f;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var t = easeOutSine(Mathf.Min(elapsed, duration), duration, 0, 1);
            _placeableSymbolObjs[ix].transform.localScale = Vector3.Lerp(new Vector3(.04f, .04f, .04f), new Vector3(.025f, .025f, .025f), t);
            _placeableSymbolObjs[ix].transform.localPosition = Vector3.Lerp(new Vector3(0, .016f, 0), new Vector3(0, .01401f, 0), t);
            yield return null;
        }
    }

    private KMSelectable.OnInteractHandler GetSelectableHandler(int ix)
    {
        return delegate
        {
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, SelectableBoxes[ix].transform);
            SelectableBoxes[ix].AddInteractionPunch();

            if (_selectableSymbols[ix] == null)
                // The user clicked on a symbol that is already on the net; allow that and do nothing.
                return false;

            var tsk = new RotateTask { From = _selectableSymbols[ix].Orientation, To = (_selectableSymbols[ix].Orientation + 1) % 4 };
            _selectableSymbols[ix] = new FaceSymbol(_selectableSymbols[ix].Symbol, tsk.To);
            _queues[ix].Enqueue(tsk);
            _lastSelectableIx = ix;
            AssignSymbols();
            return false;
        };
    }

    private void AssignSymbols()
    {
        // Selectable boxes
        for (int i = 0; i < 5; i++)
        {
            if (_selectableSymbols[i] == null)
                _selectableSymbolObjs[i].gameObject.SetActive(false);
            _selectableFrameObjs[i].material = _selectableSymbols[i] == null ? FrameBlue : FrameRed;
            _selectableScreenObjs[i].material = _selectableSymbols[i] != null && _selectableSymbols[i].Symbol == _solution[_faceGivenByHighlight].Symbol ? ScreenLight : ScreenDark;
        }

        // Placeable boxes
        for (int y = 0; y < _puzzle.Faces.GetLength(1); y++)
            for (int x = 0; x < _puzzle.Faces.GetLength(0); x++)
            {
                if (_puzzle.Faces[x, y] == null)
                    continue;
                var face = _puzzle.Faces[x, y].Face;
                var isPlaced = !_selectableSymbols.Any(sel => sel != null && sel.Symbol == _solution[face].Symbol);
                _placeableSymbolObjs[face].gameObject.SetActive(isPlaced);
                _placeableFrameObjs[face].material = isPlaced ? FrameGreen : FrameYellow;
                _placeableScreenObjs[face].material = face == _faceGivenByHighlight && !isPlaced ? ScreenLight : ScreenDark;
            }
    }

    void Log(string msg, params object[] fmtArgs)
    {
        Debug.LogFormat(@"[Pattern Cube #{0}]{1}", _moduleId, string.Format(msg, fmtArgs));
    }
}
