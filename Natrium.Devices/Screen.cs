namespace Natrium.Devices
{
// 0 -> Index (W)
// 1 -> Value (W)
// 2 -> Clear (W)
// 3 -> AutoIncrement (W)
// 4 -> Width (R)
// 5 -> Height (R)
    public class Screen : IDevice
    {
        private uint _nextIndex;

        public Screen(int width, int height)
        {
            Data = new char[width * height];
            Width = width;
            Height = height;
        }

        public char[] Data { get; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        private bool _autoIncrement;

        public bool TryReadValue(int index, out double value)
        {
            value = 0;
            switch (index)
            {
                case 1:
                    if (_nextIndex >= Data.Length)
                        return false;
                    value = Data[_nextIndex];
                    break;

                case 4:
                    value = Width;
                    break;

                case 5:
                    value = Height;
                    break;

                default:
                    return false;
            }

            return true;
        }

        public bool TryWriteValue(int index, double value)
        {
            switch (index)
            {
                case 0:
                    if (value < 0 || value >= Data.Length)
                        return false;
                    _nextIndex = (uint)value;
                    break;

                case 1:
                    if (_nextIndex >= Data.Length)
                        return false;
                    Data[_nextIndex] = (char)value;
                    if (_autoIncrement)
                        _nextIndex++;
                    break;

                case 2:
                    System.Array.Fill(Data, ' ');
                    break;
                
                case 3 :
                    _autoIncrement = value > 0;
                    break;

                default:
                    return false;
            }

            return true;
        }
    }
}