using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using CopaFormGui.Models;
using CopaFormGui.ViewModels;

namespace CopaFormGui.Views;

public partial class ProgramEditorView : System.Windows.Controls.UserControl
{
    private ProgramEditorViewModel? _vm;

    public ProgramEditorView()
    {
        InitializeComponent();
    }

    // Called by XAML Loaded="OnLoaded"
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ProgramEditorViewModel vm)
            AttachViewModel(vm);

        DataContextChanged += (_, args) =>
        {
            if (args.NewValue is ProgramEditorViewModel newVm)
                AttachViewModel(newVm);
        };
    }

    private void AttachViewModel(ProgramEditorViewModel vm)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnVmPropertyChanged;

        _vm = vm;
        _vm.PropertyChanged += OnVmPropertyChanged;

        // Build 3-D scene for whatever is already loaded
        Rebuild3DScene(_vm.Steps);
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Rebuild the 3-D scene whenever the step list is replaced or selection changes
        if ((e.PropertyName is nameof(ProgramEditorViewModel.Steps) or
                               nameof(ProgramEditorViewModel.SelectedStep)) &&
            _vm is not null)
        {
            Rebuild3DScene(_vm.Steps);
        }
    }

    // =========================================================================
    // 3-D scene builder
    // =========================================================================

    private void Rebuild3DScene(IEnumerable<PunchStep> stepsEnumerable)
    {
        // Remove all geometry models – keep the two lights (children 0 and 1)
        while (Punch3DScene.Children.Count > 2)
            Punch3DScene.Children.RemoveAt(Punch3DScene.Children.Count - 1);

        var steps = stepsEnumerable.ToList();
        if (steps.Count == 0) return;

        // ── Coordinate mapping ──────────────────────────────────────────────
        double xMin = steps.Min(s => s.X), xMax = steps.Max(s => s.X);
        double yMin = steps.Min(s => s.Y), yMax = steps.Max(s => s.Y);
        double cx   = (xMin + xMax) / 2.0;
        double cy   = (yMin + yMax) / 2.0;
        double range = Math.Max(Math.Max(xMax - xMin, yMax - yMin), 1.0);
        double scale = 8.0 / range;          // fit data into ±4 WPF units

        // ── Sheet plate ─────────────────────────────────────────────────────
        const double SheetPad   = 1.0;       // WPF units around punch cloud
        const double SheetThick = 0.25;

        double sw = (xMax - xMin) * scale + SheetPad * 2;
        double sd = (yMax - yMin) * scale + SheetPad * 2;

        var sheetMat = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(190, 190, 195)));
        var sheetModel = new GeometryModel3D
        {
            Geometry     = BuildBox(sw, SheetThick, sd),
            Material     = sheetMat,
            BackMaterial = sheetMat,
            Transform    = new TranslateTransform3D(0, -SheetThick / 2.0, 0)
        };
        Punch3DScene.Children.Add(sheetModel);

        // ── Punch cylinders ─────────────────────────────────────────────────
        const double CylRadius = 0.20;
        const double CylHeight = 0.45;

        var normalMat   = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(220, 50, 50)));
        var selectedMat = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(255, 210, 0)));

        foreach (var step in steps)
        {
            double wx = (step.X - cx) * scale;
            double wz = -(step.Y - cy) * scale;   // Y axis → −Z in WPF 3-D
            bool isSel = step == _vm?.SelectedStep;

            var mat = isSel ? selectedMat : normalMat;
            var cyl = new GeometryModel3D
            {
                Geometry     = BuildCylinder(CylRadius, CylHeight, 12),
                Material     = mat,
                BackMaterial = mat,
                Transform    = new TranslateTransform3D(wx, CylHeight / 2.0, wz)
            };
            Punch3DScene.Children.Add(cyl);
        }

        // ── Camera ──────────────────────────────────────────────────────────
        double camD = Math.Max(sw, sd) * 0.9;
        Punch3DCamera.Position       = new Point3D(0, camD, camD * 1.3);
        Punch3DCamera.LookDirection  = new Vector3D(0, -camD, -camD * 1.3);
    }

    // ── Mesh helpers ─────────────────────────────────────────────────────────

    /// <summary>Axis-aligned box centred at origin, dimensions w × h × d.</summary>
    private static MeshGeometry3D BuildBox(double w, double h, double d)
    {
        double hw = w / 2, hh = h / 2, hd = d / 2;

        var p = new[]
        {
            new Point3D(-hw, -hh, -hd), // 0
            new Point3D( hw, -hh, -hd), // 1
            new Point3D( hw,  hh, -hd), // 2
            new Point3D(-hw,  hh, -hd), // 3
            new Point3D(-hw, -hh,  hd), // 4
            new Point3D( hw, -hh,  hd), // 5
            new Point3D( hw,  hh,  hd), // 6
            new Point3D(-hw,  hh,  hd), // 7
        };

        var mesh = new MeshGeometry3D();
        foreach (var pt in p) mesh.Positions.Add(pt);

        // Six faces, each as two CCW triangles (with BackMaterial mirroring handles any winding)
        int[] idx =
        {
            0,1,2, 0,2,3,   // front  (Z = -hd)
            5,4,7, 5,7,6,   // back   (Z = +hd)
            4,0,3, 4,3,7,   // left   (X = -hw)
            1,5,6, 1,6,2,   // right  (X = +hw)
            3,2,6, 3,6,7,   // top    (Y = +hh)
            4,5,1, 4,1,0,   // bottom (Y = -hh)
        };
        foreach (var i in idx) mesh.TriangleIndices.Add(i);
        return mesh;
    }

    /// <summary>Upright cylinder centred at origin, capped top and bottom.</summary>
    private static MeshGeometry3D BuildCylinder(double radius, double height, int sides)
    {
        var mesh = new MeshGeometry3D();
        double hh = height / 2;

        // Centre caps
        mesh.Positions.Add(new Point3D(0, -hh, 0)); // 0 – bottom centre
        mesh.Positions.Add(new Point3D(0,  hh, 0)); // 1 – top centre

        // Rim vertices (bottom then top, interleaved)
        for (int i = 0; i < sides; i++)
        {
            double a = 2 * Math.PI * i / sides;
            double x = radius * Math.Cos(a);
            double z = radius * Math.Sin(a);
            mesh.Positions.Add(new Point3D(x, -hh, z)); // 2 + 2*i   bottom rim
            mesh.Positions.Add(new Point3D(x,  hh, z)); // 2 + 2*i+1 top rim
        }

        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            int b0 = 2 + 2 * i,    b1 = 2 + 2 * next;
            int t0 = b0 + 1,        t1 = b1 + 1;

            // Side quad
            mesh.TriangleIndices.Add(b0); mesh.TriangleIndices.Add(b1); mesh.TriangleIndices.Add(t0);
            mesh.TriangleIndices.Add(t0); mesh.TriangleIndices.Add(b1); mesh.TriangleIndices.Add(t1);
            // Bottom cap
            mesh.TriangleIndices.Add(0);  mesh.TriangleIndices.Add(b1); mesh.TriangleIndices.Add(b0);
            // Top cap
            mesh.TriangleIndices.Add(1);  mesh.TriangleIndices.Add(t0); mesh.TriangleIndices.Add(t1);
        }
        return mesh;
    }
}
