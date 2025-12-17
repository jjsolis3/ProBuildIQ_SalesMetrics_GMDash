//Services/Mvc/RazorViewToStringRenderer.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace SalesMetrics.Services.Mvc;

public interface IRazorViewToStringRenderer
{
    Task<string> RenderAsync(ControllerContext ctx, string viewPath, object model);
}

public sealed class RazorViewToStringRenderer : IRazorViewToStringRenderer
{
    private readonly IRazorViewEngine _viewEngine;
    private readonly ITempDataProvider _tempDataProvider;
    private readonly IServiceProvider _sp;

    public RazorViewToStringRenderer(IRazorViewEngine viewEngine, ITempDataProvider tempDataProvider, IServiceProvider sp)
    {
        _viewEngine = viewEngine; _tempDataProvider = tempDataProvider; _sp = sp;
    }

    public async Task<string> RenderAsync(ControllerContext ctx, string viewPath, object model)
    {
        var result = _viewEngine.GetView(executingFilePath: null, viewPath: viewPath, isMainPage: false);
        if (!result.Success) throw new InvalidOperationException($"View '{viewPath}' not found.");

        var view = result.View;
        await using var sw = new StringWriter();
        var vdc = new ViewDataDictionary(new EmptyModelMetadataProvider(), ctx.ModelState) { Model = model };
        var tdc = new TempDataDictionary(ctx.HttpContext, _tempDataProvider);

        var vcc = new ViewContext(ctx, view, vdc, tdc, sw, new HtmlHelperOptions());
        await view.RenderAsync(vcc);
        return sw.ToString();
    }
}
