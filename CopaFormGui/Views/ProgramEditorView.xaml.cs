using System.Collections.Specialized;
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
    private INotifyCollectionChanged? _stepsCollection;

    public ProgramEditorView()
    {
        InitializeComponent();
    }

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
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
            DetachStepsCollectionHandler();
        }

        _vm = vm;
        _vm.PropertyChanged += OnVmPropertyChanged;
        AttachStepsCollectionHandler(_vm.Steps);
        SafeRebuild3DScene();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_vm is null)
            return;

        if (e.PropertyName is nameof(ProgramEditorViewModel.Steps) or nameof(ProgramEditorViewModel.SelectedStep))
        {
            if (e.PropertyName == nameof(ProgramEditorViewModel.Steps))
                AttachStepsCollectionHandler(_vm.Steps);

            SafeRebuild3DScene();
        }
    }

    private void AttachStepsCollectionHandler(IEnumerable<PunchStep> steps)
    {
        DetachStepsCollectionHandler();
        if (steps is INotifyCollectionChanged notifyCollectionChanged)
        {
            _stepsCollection = notifyCollectionChanged;
            _stepsCollection.CollectionChanged += OnStepsCollectionChanged;
        }
    }

    private void DetachStepsCollectionHandler()
    {
        if (_stepsCollection is not null)
            _stepsCollection.CollectionChanged -= OnStepsCollectionChanged;
        _stepsCollection = null;
    }

    private void OnStepsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SafeRebuild3DScene();
    }

    private void SafeRebuild3DScene()
    {
        if (_vm is null)
            return;

        try
        {
            Rebuild3DScene(_vm.Steps);
        }
        catch (Exception ex)
        {
            _vm.StatusMessage = $"Program preview error: {ex.Message}";
            App.LogException("ProgramEditorView.SafeRebuild3DScene", ex);
        }
    }

    private void Rebuild3DScene(IEnumerable<PunchStep> stepsEnumerable)
    {
        if (Punch3DScene is null || Punch3DCamera is null)
            return;

        while (Punch3DScene.Children.Count > 2)
            Punch3DScene.Children.RemoveAt(Punch3DScene.Children.Count - 1);

        var steps = stepsEnumerable.ToList();
        if (steps.Count == 0)
            return;

        double xMin = steps.Min(s => s.X), xMax = steps.Max(s => s.X);
        double yMin = steps.Min(s => s.Y), yMax = steps.Max(s => s.Y);
        double cx = (xMin + xMax) / 2.0;
        double cy = (yMin + yMax) / 2.0;
        double range = Math.Max(Math.Max(xMax - xMin, yMax - yMin), 1.0);
        double scale = 8.0 / range;

        const double sheetPad = 1.0;
        const double sheetThick = 0.25;
        double sw = (xMax - xMin) * scale + sheetPad * 2;
        double sd = (yMax - yMin) * scale + sheetPad * 2;

        var sheetMat = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(190, 190, 195)));
        var sheetModel = new GeometryModel3D
        {
            Geometry = BuildBox(sw, sheetThick, sd),
            Material = sheetMat,
            BackMaterial = sheetMat,
            Transform = new TranslateTransform3D(0, -sheetThick / 2.0, 0)
        };
        Punch3DScene.Children.Add(sheetModel);

        const double cylRadius = 0.20;
        const double cylHeight = 0.45;
        var normalMat = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(220, 50, 50)));
        var selectedMat = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(255, 210, 0)));

        foreach (var step in steps)
        {
            double wx = (step.X - cx) * scale;
            double wz = -(step.Y - cy) * scale;
            bool isSel = step == _vm?.SelectedStep;

            var mat = isSel ? selectedMat : normalMat;
            var cyl = new GeometryModel3D
            {
                Geometry = BuildCylinder(cylRadius, cylHeight, 12),
                Material = mat,
                BackMaterial = mat,
                Transform = new TranslateTransform3D(wx, cylHeight / 2.0, wz)
            };
            Punch3DScene.Children.Add(cyl);
        }

        double camD = Math.Max(sw, sd) * 0.9;
        Punch3DCamera.Position = new Point3D(0, camD, camD * 1.3);
        Punch3DCamera.LookDirection = new Vector3D(0, -camD, -camD * 1.3);
    }

    private static MeshGeometry3D BuildBox(double w, double h, double d)
    {
        double hw = w / 2, hh = h / 2, hd = d / 2;
        var p = new[]
        {
            new Point3D(-hw, -hh, -hd), new Point3D(hw, -hh, -hd),
            new Point3D(hw, hh, -hd), new Point3D(-hw, hh, -hd),
            new Point3D(-hw, -hh, hd), new Point3D(hw, -hh, hd),
            new Point3D(hw, hh, hd), new Point3D(-hw, hh, hd)
        };

        var mesh = new MeshGeometry3D();
        foreach (var pt in p) mesh.Positions.Add(pt);

        int[] idx =
        {
            0,1,2, 0,2,3,
            5,4,7, 5,7,6,
            4,0,3, 4,3,7,
            1,5,6, 1,6,2,
            3,2,6, 3,6,7,
            4,5,1, 4,1,0
        };
        foreach (var i in idx) mesh.TriangleIndices.Add(i);
        return mesh;
    }

    private static MeshGeometry3D BuildCylinder(double radius, double height, int sides)
    {
        var mesh = new MeshGeometry3D();
        double hh = height / 2;

        mesh.Positions.Add(new Point3D(0, -hh, 0));
        mesh.Positions.Add(new Point3D(0, hh, 0));

        for (int i = 0; i < sides; i++)
        {
            double a = 2 * Math.PI * i / sides;
            double x = radius * Math.Cos(a);
            double z = radius * Math.Sin(a);
            mesh.Positions.Add(new Point3D(x, -hh, z));
            mesh.Positions.Add(new Point3D(x, hh, z));
        }

        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            int b0 = 2 + 2 * i, b1 = 2 + 2 * next;
            int t0 = b0 + 1, t1 = b1 + 1;

            mesh.TriangleIndices.Add(b0); mesh.TriangleIndices.Add(b1); mesh.TriangleIndices.Add(t0);
            mesh.TriangleIndices.Add(t0); mesh.TriangleIndices.Add(b1); mesh.TriangleIndices.Add(t1);
            mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(b1); mesh.TriangleIndices.Add(b0);
            mesh.TriangleIndices.Add(1); mesh.TriangleIndices.Add(t0); mesh.TriangleIndices.Add(t1);
        }

        return mesh;
    }
}
