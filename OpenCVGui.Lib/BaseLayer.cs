using OpenCvSharp;
using System.Diagnostics;
using System.Drawing.Text;

namespace OpenCVGui.Lib;

public abstract class BaseLayer
{
    /// <summary>
    /// runtime of last render call ms
    /// </summary>
    public long LastRuntime { get; protected set; }


    /// <summary>
    /// can expect input and returns output
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    protected abstract Mat RenderInternal(Mat? input);

    public Mat Render(Mat? input)
    {
        var watch = Stopwatch.StartNew();
        var result = RenderInternal(input);
        LastRuntime = watch.ElapsedMilliseconds;
        return result;
    }

}
