using Avalonia.Input;
using SharpHook.Data;

namespace WordLens.Util
{
    public class KeyCodeUtil
    {
        public static KeyCode ConvertToKeyCode(Key key)
        {
            return key switch
            {
                Key.A => KeyCode.VcA,
                Key.B => KeyCode.VcB,
                Key.C => KeyCode.VcC,
                Key.D => KeyCode.VcD,
                Key.E => KeyCode.VcE,
                Key.F => KeyCode.VcF,
                Key.G => KeyCode.VcG,
                Key.H => KeyCode.VcH,
                Key.I => KeyCode.VcI,
                Key.J => KeyCode.VcJ,
                Key.K => KeyCode.VcK,
                Key.L => KeyCode.VcL,
                Key.M => KeyCode.VcM,
                Key.N => KeyCode.VcN,
                Key.O => KeyCode.VcO,
                Key.P => KeyCode.VcP,
                Key.Q => KeyCode.VcQ,
                Key.R => KeyCode.VcR,
                Key.S => KeyCode.VcS,
                Key.T => KeyCode.VcT,
                Key.U => KeyCode.VcU,
                Key.V => KeyCode.VcV,
                Key.W => KeyCode.VcW,
                Key.X => KeyCode.VcX,
                Key.Y => KeyCode.VcY,
                Key.Z => KeyCode.VcZ,
                Key.D0 => KeyCode.Vc0,
                Key.D1 => KeyCode.Vc1,
                Key.D2 => KeyCode.Vc2,
                Key.D3 => KeyCode.Vc3,
                Key.D4 => KeyCode.Vc4,
                Key.D5 => KeyCode.Vc5,
                Key.D6 => KeyCode.Vc6,
                Key.D7 => KeyCode.Vc7,
                Key.D8 => KeyCode.Vc8,
                Key.D9 => KeyCode.Vc9,
                Key.F1 => KeyCode.VcF1,
                Key.F2 => KeyCode.VcF2,
                Key.F3 => KeyCode.VcF3,
                Key.F4 => KeyCode.VcF4,
                Key.F5 => KeyCode.VcF5,
                Key.F6 => KeyCode.VcF6,
                Key.F7 => KeyCode.VcF7,
                Key.F8 => KeyCode.VcF8,
                Key.F9 => KeyCode.VcF9,
                Key.F10 => KeyCode.VcF10,
                Key.F11 => KeyCode.VcF11,
                Key.F12 => KeyCode.VcF12,
                Key.Space => KeyCode.VcSpace,
                Key.Enter => KeyCode.VcEnter,
                _ => KeyCode.VcUndefined
            };
        }

        public static string GetKeyName(KeyCode keyCode)
        {
            return keyCode switch
            {
                KeyCode.VcA => "A",
                KeyCode.VcB => "B",
                KeyCode.VcC => "C",
                KeyCode.VcD => "D",
                KeyCode.VcE => "E",
                KeyCode.VcF => "F",
                KeyCode.VcG => "G",
                KeyCode.VcH => "H",
                KeyCode.VcI => "I",
                KeyCode.VcJ => "J",
                KeyCode.VcK => "K",
                KeyCode.VcL => "L",
                KeyCode.VcM => "M",
                KeyCode.VcN => "N",
                KeyCode.VcO => "O",
                KeyCode.VcP => "P",
                KeyCode.VcQ => "Q",
                KeyCode.VcR => "R",
                KeyCode.VcS => "S",
                KeyCode.VcT => "T",
                KeyCode.VcU => "U",
                KeyCode.VcV => "V",
                KeyCode.VcW => "W",
                KeyCode.VcX => "X",
                KeyCode.VcY => "Y",
                KeyCode.VcZ => "Z",
                KeyCode.Vc0 => "0",
                KeyCode.Vc1 => "1",
                KeyCode.Vc2 => "2",
                KeyCode.Vc3 => "3",
                KeyCode.Vc4 => "4",
                KeyCode.Vc5 => "5",
                KeyCode.Vc6 => "6",
                KeyCode.Vc7 => "7",
                KeyCode.Vc8 => "8",
                KeyCode.Vc9 => "9",
                KeyCode.VcF1 => "F1",
                KeyCode.VcF2 => "F2",
                KeyCode.VcF3 => "F3",
                KeyCode.VcF4 => "F4",
                KeyCode.VcF5 => "F5",
                KeyCode.VcF6 => "F6",
                KeyCode.VcF7 => "F7",
                KeyCode.VcF8 => "F8",
                KeyCode.VcF9 => "F9",
                KeyCode.VcF10 => "F10",
                KeyCode.VcF11 => "F11",
                KeyCode.VcF12 => "F12",
                KeyCode.VcSpace => "Space",
                KeyCode.VcEnter => "Enter",
                _ => keyCode.ToString()
            };
        }
    }
}