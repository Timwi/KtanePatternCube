using System;
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
    private readonly List<int> _facesRevealed = new List<int>();
    private Dictionary<char, Texture> _symbolTextures;

    void Start()
    {
        _moduleId = _moduleIdCounter++;

        for (int i = 0; i < 5; i++)
        {
            _selectableSymbolObjs[i] = SelectableBoxes[i].transform.Find("Symbol").GetComponent<MeshRenderer>();
            _selectableFrameObjs[i] = SelectableBoxes[i].transform.Find("Frame").GetComponent<MeshRenderer>();
            _selectableScreenObjs[i] = SelectableBoxes[i].transform.Find("Screen").GetComponent<MeshRenderer>();
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
        _selectableSymbols = _solution.Except(new[] { _solution[faceGivenFully], _solution[_faceGivenByHighlight] }).ToArray().Shuffle().Concat(new[] { _solution[_faceGivenByHighlight] }).ToArray();
        for (int i = 0; i < _selectableSymbols.Length; i++)
            _selectableSymbols[i] = new FaceSymbol(_selectableSymbols[i].Symbol, Rnd.Range(0, 4));

        // Populate the selectable boxes
        MainSelectable.Children = new KMSelectable[25];
        for (int i = 0; i < 5; i++)
        {
            MainSelectable.Children[i * 5] = SelectableBoxes[i];
            _selectableSymbolObjs[i].material.mainTexture = _symbolTextures[_selectableSymbols[i].Symbol];
            _selectableSymbolObjs[i].transform.eulerAngles = new Vector3(90, _selectableSymbols[i].Orientation * 90, 0);
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
                MainSelectable.Children[(5 - _puzzle.Faces.GetLength(1) + y) * 5 + x] = PlaceableBoxes[fi.Face];
            }

        MainSelectable.UpdateChildren(SelectableBoxes[0]);

        // Set the correct mesh for the module front
        var id = "ModuleFront_" + _puzzle.ID;
        ModuleFront.mesh = NetMeshes.First(m => m.name == id);

        Log("Puzzle=" + Enumerable.Range(0, _puzzle.Faces.GetLength(1)).Select(y => Enumerable.Range(0, _puzzle.Faces.GetLength(0)).Select(x =>
            _puzzle.Faces[x, y] == null || _puzzle.Faces[x, y].Face == faceGivenFully ? str(_puzzle.Faces[x, y]) :
            _puzzle.Faces[x, y].Face == _faceGivenByHighlight ? _solution[_faceGivenByHighlight].Symbol.ToString() : "?").JoinString(";")).JoinString("|"));
        Log("Symbols=" + _solution.Select(s => s.Symbol).JoinString());
        Log("Solution=" + Enumerable.Range(0, _puzzle.Faces.GetLength(1)).Select(y => Enumerable.Range(0, _puzzle.Faces.GetLength(0)).Select(x => str(_puzzle.Faces[x, y])).JoinString(";")).JoinString("|"));
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
                Log("You tried to place a symbol without selecting a symbol first. Strike.");
                Module.HandleStrike();
            }
            else if (_selectableSymbols[_lastSelectableIx] == null)
            {
                Log("You tried to place a symbol that you already placed. Strike.");
                Module.HandleStrike();
            }
            else
            {
                var sym = _selectableSymbols[_lastSelectableIx];
                if (sym.Symbol != _solution[fi.Face].Symbol)
                {
                    Log("You tried to place symbol {0} where symbol {1} should have gone. Strike.", sym.Symbol, _solution[fi.Face].Symbol);
                    Module.HandleStrike();
                }
                else if (sym.Orientation != (_solution[fi.Face].Orientation + fi.Orientation) % 4)
                {
                    Log("You tried to place symbol {0} in the correct place, but wrong orientation ({1} instead of {2}). Strike.", sym.Symbol, sym.Orientation, (_solution[fi.Face].Orientation + fi.Orientation) % 4);
                    Module.HandleStrike();
                }
                else
                {
                    // Correct placement
                    Log("Symbol {0} placed correctly.", sym.Symbol);
                    _selectableSymbols[_lastSelectableIx] = null;
                    AssignSymbols();
                    if (_selectableSymbols.All(s => s == null))
                    {
                        Log("Module solved.");
                        Module.HandlePass();
                    }
                }
            }
            return false;
        };
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

            _selectableSymbols[ix] = new FaceSymbol(_selectableSymbols[ix].Symbol, (_selectableSymbols[ix].Orientation + 1) % 4);
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
            if (_selectableSymbols[i] != null)
                _selectableSymbolObjs[i].transform.eulerAngles = new Vector3(90, _selectableSymbols[i].Orientation * 90, 0);
            else
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
        Debug.LogFormat(@"[Pattern Cube #{0}] {1}", _moduleId, string.Format(msg, fmtArgs));
    }
}
