namespace Shadowsocks.Model
{
    public enum LoadBalance
    {
        OneByOne,
        Random,
        FastDownloadSpeed,
        LowLatency,
        LowException,
        SelectedFirst,
        Timer,
    }
}
