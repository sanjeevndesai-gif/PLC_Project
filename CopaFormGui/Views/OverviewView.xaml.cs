using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using CopaFormGui.ViewModels;

namespace CopaFormGui.Views;

public partial class OverviewView : System.Windows.Controls.UserControl
{
    private OverviewViewModel? _vm;
    private bool _isRotating3D;
    private Point _lastMousePosition;
    private double _cameraYawDeg = 0;
    private double _cameraPitchDeg = -28;
    private double _cameraDistance = 13.5;

    public OverviewView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachViewModel(DataContext as OverviewViewModel);
        UpdateCamera();
        SafeRebuild3DScene();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm.ToolPreviewShapes.CollectionChanged -= OnToolPreviewShapesChanged;
        }

        AttachViewModel(e.NewValue as OverviewViewModel);
        UpdateCamera();
        SafeRebuild3DScene();
    }

    private void AttachViewModel(OverviewViewModel? vm)
    {
        _vm = vm;
        if (_vm is null) return;
        _vm.PropertyChanged += OnVmPropertyChanged;
        _vm.ToolPreviewShapes.CollectionChanged += OnToolPreviewShapesChanged;
    }

    private void OnToolPreviewShapesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        SafeRebuild3DScene();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(OverviewViewModel.PreviewSheetLeft)
            or nameof(OverviewViewModel.PreviewSheetTop)
            or nameof(OverviewViewModel.PreviewSheetWidth)
            or nameof(OverviewViewModel.PreviewSheetHeight)
            or nameof(OverviewViewModel.ToolPreviewShapes))
        {
            if (e.PropertyName == nameof(OverviewViewModel.ToolPreviewShapes) && _vm is not null)
            {
                _vm.ToolPreviewShapes.CollectionChanged -= OnToolPreviewShapesChanged;
                _vm.ToolPreviewShapes.CollectionChanged += OnToolPreviewShapesChanged;
            }
            SafeRebuild3DScene();
        }
    }

    private void SafeRebuild3DScene()
    {
        try
        {
            Rebuild3DScene();
        }
        catch (Exception ex)
        {
            App.LogException("OverviewView.SafeRebuild3DScene", ex);
        }
    }

    private void Rebuild3DScene()
    {
        if (_vm is null || Overview3DScene is null)
            return;

        while (Overview3DScene.Children.Count > 2)
            Overview3DScene.Children.RemoveAt(2);

        const double plateWidth = 12.0;
        const double plateThickness = 0.35;
        var aspect = _vm.PreviewSheetHeight <= 0 ? 0.6 : (_vm.PreviewSheetHeight / _vm.PreviewSheetWidth);
        var plateDepth = Clamp(plateWidth * aspect, 5.5, 10.5);

        var plate = CreateBox(
            centerX: 0,
            centerY: 0,
            centerZ: 0,
            sizeX: plateWidth,
            sizeY: plateThickness,
            sizeZ: plateDepth,
            material: new DiffuseMaterial(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D7C9B6"))));
        Overview3DScene.Children.Add(plate);

        var markerMat = new DiffuseMaterial(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C75A00")));
        foreach (var shape in _vm.ToolPreviewShapes)
        {
            var centerXCanvas = shape.CanvasLeft + (shape.ShapeWidth / 2);
            var centerYCanvas = shape.CanvasTop + (shape.ShapeHeight / 2);

            var nx = (_vm.PreviewSheetWidth <= 0) ? 0.5 : ((centerXCanvas - _vm.PreviewSheetLeft) / _vm.PreviewSheetWidth);
            var ny = (_vm.PreviewSheetHeight <= 0) ? 0.5 : ((centerYCanvas - _vm.PreviewSheetTop) / _vm.PreviewSheetHeight);
            nx = Clamp(nx, 0, 1);
            ny = Clamp(ny, 0, 1);

            var x = (nx - 0.5) * plateWidth;
            var z = (ny - 0.5) * plateDepth;

            var rx = Clamp((shape.ShapeWidth / _vm.PreviewSheetWidth) * plateWidth * 0.5, 0.08, 0.35);
            var rz = Clamp((shape.ShapeHeight / _vm.PreviewSheetHeight) * plateDepth * 0.5, 0.08, 0.35);

            GeometryModel3D marker = shape.IsRound
                ? CreateCylinder(x, plateThickness * 0.5 + 0.06, z, Math.Max(rx, rz), 0.12, markerMat)
                : CreateBox(x, plateThickness * 0.5 + 0.06, z, rx * 2, 0.12, rz * 2, markerMat);

            Overview3DScene.Children.Add(marker);
        }

        UpdateCamera();
    }

    private void Overview3DViewport_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        _ = sender;
        _cameraDistance = Clamp(_cameraDistance - (e.Delta * 0.01), 6.0, 30.0);
        UpdateCamera();
        e.Handled = true;
    }

    private void Overview3DViewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not IInputElement inputElement) return;

        _isRotating3D = true;
        _lastMousePosition = e.GetPosition(this);
        Mouse.Capture(inputElement);
        Mouse.OverrideCursor = Cursors.SizeAll;
        e.Handled = true;
    }

    private void Overview3DViewport_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isRotating3D) return;

        var pos = e.GetPosition(this);
        var dx = pos.X - _lastMousePosition.X;
        var dy = pos.Y - _lastMousePosition.Y;
        _lastMousePosition = pos;

        _cameraYawDeg += dx * 0.45;
        _cameraPitchDeg = Clamp(_cameraPitchDeg - (dy * 0.35), -75, 10);
        UpdateCamera();
    }

    private void Overview3DViewport_MouseLeftButtonUp(object sender, MouseEventArgs e)
    {
        _ = sender;
        _ = e;
        if (!_isRotating3D) return;

        _isRotating3D = false;
        Mouse.Capture(null);
        Mouse.OverrideCursor = null;
    }

    private void UpdateCamera()
    {
        if (Overview3DCamera is null) return;

        var yawRad = _cameraYawDeg * (Math.PI / 180.0);
        var pitchRad = _cameraPitchDeg * (Math.PI / 180.0);

        var x = _cameraDistance * Math.Cos(pitchRad) * Math.Sin(yawRad);
        var y = _cameraDistance * Math.Sin(-pitchRad);
        var z = _cameraDistance * Math.Cos(pitchRad) * Math.Cos(yawRad);

        var position = new Point3D(x, y, z);
        var target = new Point3D(0, 0, 0);

        Overview3DCamera.Position = position;
        Overview3DCamera.LookDirection = target - position;
        Overview3DCamera.UpDirection = new Vector3D(0, 1, 0);
        Overview3DCamera.FieldOfView = 48;
    }

    private static GeometryModel3D CreateBox(double centerX, double centerY, double centerZ, double sizeX, double sizeY, double sizeZ, Material material)
    {
        var hx = sizeX / 2;
        var hy = sizeY / 2;
        var hz = sizeZ / 2;

        var p000 = new Point3D(centerX - hx, centerY - hy, centerZ - hz);
        var p001 = new Point3D(centerX - hx, centerY - hy, centerZ + hz);
        var p010 = new Point3D(centerX - hx, centerY + hy, centerZ - hz);
        var p011 = new Point3D(centerX - hx, centerY + hy, centerZ + hz);
        var p100 = new Point3D(centerX + hx, centerY - hy, centerZ - hz);
        var p101 = new Point3D(centerX + hx, centerY - hy, centerZ + hz);
        var p110 = new Point3D(centerX + hx, centerY + hy, centerZ - hz);
        var p111 = new Point3D(centerX + hx, centerY + hy, centerZ + hz);

        var mesh = new MeshGeometry3D();
        AddQuad(mesh, p000, p100, p110, p010);
        AddQuad(mesh, p001, p011, p111, p101);
        AddQuad(mesh, p000, p010, p011, p001);
        AddQuad(mesh, p100, p101, p111, p110);
        AddQuad(mesh, p010, p110, p111, p011);
        AddQuad(mesh, p000, p001, p101, p100);

        return new GeometryModel3D(mesh, material) { BackMaterial = material };
    }

    private static GeometryModel3D CreateCylinder(double centerX, double centerY, double centerZ, double radius, double height, Material material)
    {
        const int segments = 24;
        var mesh = new MeshGeometry3D();
        var y0 = centerY - (height / 2);
        var y1 = centerY + (height / 2);

        for (int i = 0; i < segments; i++)
        {
            var a0 = 2 * Math.PI * i / segments;
            var a1 = 2 * Math.PI * (i + 1) / segments;

            var p0 = new Point3D(centerX + radius * Math.Cos(a0), y0, centerZ + radius * Math.Sin(a0));
            var p1 = new Point3D(centerX + radius * Math.Cos(a1), y0, centerZ + radius * Math.Sin(a1));
            var p2 = new Point3D(centerX + radius * Math.Cos(a1), y1, centerZ + radius * Math.Sin(a1));
            var p3 = new Point3D(centerX + radius * Math.Cos(a0), y1, centerZ + radius * Math.Sin(a0));

            AddQuad(mesh, p0, p1, p2, p3);
            AddTriangle(mesh, new Point3D(centerX, y1, centerZ), p3, p2);
            AddTriangle(mesh, new Point3D(centerX, y0, centerZ), p1, p0);
        }

        return new GeometryModel3D(mesh, material) { BackMaterial = material };
    }

    private static void AddQuad(MeshGeometry3D mesh, Point3D p0, Point3D p1, Point3D p2, Point3D p3)
    {
        var i0 = mesh.Positions.Count;
        mesh.Positions.Add(p0);
        mesh.Positions.Add(p1);
        mesh.Positions.Add(p2);
        mesh.Positions.Add(p3);
        mesh.TriangleIndices.Add(i0);
        mesh.TriangleIndices.Add(i0 + 1);
        mesh.TriangleIndices.Add(i0 + 2);
        mesh.TriangleIndices.Add(i0);
        mesh.TriangleIndices.Add(i0 + 2);
        mesh.TriangleIndices.Add(i0 + 3);
    }

    private static void AddTriangle(MeshGeometry3D mesh, Point3D p0, Point3D p1, Point3D p2)
    {
        var i0 = mesh.Positions.Count;
        mesh.Positions.Add(p0);
        mesh.Positions.Add(p1);
        mesh.Positions.Add(p2);
        mesh.TriangleIndices.Add(i0);
        mesh.TriangleIndices.Add(i0 + 1);
        mesh.TriangleIndices.Add(i0 + 2);
    }

    private static double Clamp(double value, double min, double max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}
