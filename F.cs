namespace reflex
{
    public class F
    {
        private int i1;
        private int i2;
        private int i3;
        private int i4;
        private int i5;
        public int[] mas;

        public F()
        {
            i1 = 1;
            i2 = 2;
            i3 = 3;
            i4 = 4;
            i5 = 5;
            mas = new[] { 1, 2 };
        }

        public F Get() => new F();
    }
}
