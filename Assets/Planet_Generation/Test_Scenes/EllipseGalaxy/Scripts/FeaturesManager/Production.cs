namespace ProductionRules
{
    [System.Serializable]
    public struct Production
    {
        public char symbol;
        public string replacement;

        public Production(char symbol_, string replacement_)
        {
            symbol = symbol_;
            replacement = replacement_;
        }
    }
}
