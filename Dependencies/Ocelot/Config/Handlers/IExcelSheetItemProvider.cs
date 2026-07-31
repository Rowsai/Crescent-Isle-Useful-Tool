using Lumina.Excel;

namespace Ocelot.Config.Handlers;

public interface IExcelSheetItemProvider<T>
    where T : struct, IExcelRow<T>
{
    bool Filter(T item);

    string GetLabel(T item);
}
