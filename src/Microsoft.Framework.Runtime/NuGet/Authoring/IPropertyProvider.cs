namespace NuGet
{
    public interface IPropertyProvider
    {
        string GetPropertyValue(string propertyName);
    }
}
