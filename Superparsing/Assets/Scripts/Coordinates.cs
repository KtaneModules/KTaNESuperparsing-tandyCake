using System;

public class Coordinate
{
    public int i;
    public int j;
    public Coordinate(int I, int J)
    {
        if (I < 0)
            throw new ArgumentOutOfRangeException("I");
        if (J < 0)
            throw new ArgumentOutOfRangeException("J");
        i = I;
        j = J;
    }
    public string ToString() 
    {
        return "(" + i + ", " + j + ")";
    }
    public string ToLetters()
    {
        string alphapart = "";
        int input = i;
        do
        {
            alphapart += "ABCDEFGHIJKLMNOPQRSTUVWXYZ"[input % 26];
            input /= 26;
        } while (input != 0);
        return alphapart + (j + 1);
    }

    private string NumToLetters(int input)
    {
        string output = "";
        while (input != 0)
        {
            output += "ABCDEFGHIJKLMNOPQRSTUVWXYZ"[input % 26];
            input /= 26;
        }
        return output;
    }

    public static Coordinate operator %(Coordinate A, int B) { return new Coordinate((A.i % B + B) % B, (A.j % B + B) % B);}
    public static Coordinate operator %(Coordinate A, Coordinate B) { return new Coordinate((A.i % B.i + B.i) % B.i, (A.j % B.j + B.j) % B.j); }
    public static bool operator ==(Coordinate A, Coordinate B) { return A.i == B.i && A.j == B.j; }
    public static bool operator !=(Coordinate A, Coordinate B) { return A.i != B.i || A.j != B.j; }
}

public static class CoordinateIndexing
{
    public static T ElementAt<T>(this T[,] array, Coordinate coord)
    {
        if (array == null)
            throw new ArgumentNullException("array");
        if (coord == null)
            throw new ArgumentNullException("coord");
        int[] lengths = new int[] { array.GetLength(0), array.GetLength(1) };
        if (coord.i >= array.GetLength(0))
            throw new ArgumentOutOfRangeException("coord.i");
        if (coord.j >= array.GetLength(1))
            throw new ArgumentOutOfRangeException("coord.j");
        return array[coord.i, coord.j];
    }
    public static Coordinate IndexOf(this object[,] array, object item)
    {
        int width = array.GetLength(0);
        for (int i = 0; i < array.Length; i++)
            if (array[i / width, i % width] == item)
                return new Coordinate(i / width, i % width);
        return null;
    }
}