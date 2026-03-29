namespace CopaFormGui.Models;


public class PunchProgram
{
    public int ProgramId { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Material { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public double Length { get; set; }
    public double Width { get; set; }
    public double Thickness { get; set; }
    public string ReferenceBending { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime ModifiedDate { get; set; } = DateTime.Now;
    public string CreatedBy { get; set; } = string.Empty;
    public List<PunchStep> Steps { get; set; } = new();
    public int TotalStrokes => Steps.Count;
    public int CompletedStrokes => Steps.Count(s => s.IsCompleted);
}
