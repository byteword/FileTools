namespace FileTools;

internal sealed record ComboOption<T>(string Text, T Value)
{
    public override string ToString()
    {
        return Text;
    }
}
