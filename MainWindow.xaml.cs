using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Windows;
using Microsoft.Win32;

namespace ElGamalLab
{
    public partial class MainWindow : Window
    {
        // Глобальные переменные
        private BigInteger _p;
        private BigInteger _x; // Секретный ключ
        private BigInteger _k; // Случайное число k
        private BigInteger _g; // Генератор
        private BigInteger _y; // Открытый ключ y = g^x mod p

        private string _sourceFilePath = "";

        public MainWindow()
        {
            InitializeComponent();
            LbRoots.SelectionChanged += LbRoots_SelectionChanged;
        }

        #region Математика

        private BigInteger FastExp(BigInteger baseVal, BigInteger exp, BigInteger mod)
        {
            if (mod == 1) return 0;
            return BigInteger.ModPow(baseVal, exp, mod);
        }

        private bool IsPrime(BigInteger n)
        {
            if (n <= 1) return false;
            if (n <= 3) return true;
            if (n % 2 == 0 || n % 3 == 0) return false;
            for (long i = 5; i * i <= (long)n; i += 6)
            {
                if (n % i == 0 || n % (i + 2) == 0) return false;
            }
            return true;
        }

        private List<BigInteger> GetPrimeFactors(BigInteger n)
        {
            List<BigInteger> factors = new List<BigInteger>();
            BigInteger d = 2;
            BigInteger temp = n;
            while (d * d <= temp)
            {
                if (temp % d == 0)
                {
                    factors.Add(d);
                    while (temp % d == 0) temp /= d;
                }
                d++;
            }
            if (temp > 1) factors.Add(temp);
            return factors;
        }

        private List<BigInteger> FindAllPrimitiveRoots(BigInteger p)
        {
            List<BigInteger> roots = new List<BigInteger>();
            BigInteger phi = p - 1;
            List<BigInteger> primeFactors = GetPrimeFactors(phi);

            for (BigInteger g = 2; g < p; g++)
            {
                bool isRoot = true;
                foreach (BigInteger q in primeFactors)
                {
                    BigInteger exponent = phi / q;
                    if (FastExp(g, exponent, p) == 1)
                    {
                        isRoot = false;
                        break;
                    }
                }
                if (isRoot) roots.Add(g);
            }
            return roots;
        }

        private BigInteger ModInverse(BigInteger a, BigInteger m)
        {
            BigInteger m0 = m;
            BigInteger y = 0, x = 1;
            if (m == 1) return 0;
            while (a > 1)
            {
                BigInteger q = a / m;
                BigInteger t = m;
                m = a % m;
                a = t;
                t = y;
                y = x - q * y;
                x = t;
            }
            if (x < 0) x += m0;
            return x;
        }

        #endregion

        #region UI Events

        private void BtnCalculateRoots_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _g = 0;
                _y = 0;
                TxtSelectedG.Text = "Выберите g из списка выше";
                LbRoots.Items.Clear();

                if (!BigInteger.TryParse(TxtP.Text, out _p) || _p <= 256)
                {
                    MessageBox.Show("Введите корректное простое число p > 255", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!IsPrime(_p))
                {
                    MessageBox.Show("Число p не является простым!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var roots = FindAllPrimitiveRoots(_p);
                if (roots.Count == 0)
                {
                    TxtLog.AppendText("Первообразные корни не найдены.\n");
                    return;
                }

                foreach (var root in roots)
                {
                    LbRoots.Items.Add(root.ToString());
                }

                TxtLog.AppendText($"Для p={_p} найдено {roots.Count} первообразных корней.\n");

                if (LbRoots.Items.Count > 0)
                {
                    LbRoots.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
        }

        private void LbRoots_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateGFromSelection();
        }

        private void UpdateGFromSelection()
        {
            if (LbRoots.SelectedItem != null)
            {
                string selected = LbRoots.SelectedItem.ToString();
                if (BigInteger.TryParse(selected, out _g))
                {
                    TxtSelectedG.Text = $"Выбран g = {_g}";
                    TxtSelectedG.Foreground = System.Windows.Media.Brushes.Black;
                    if (BigInteger.TryParse(TxtX.Text, out _x)) ValidateKeys();
                }
            }
            else
            {
                _g = 0;
                TxtSelectedG.Text = "Выберите g";
            }
        }

        private void ValidateKeys()
        {
            if (_g == 0 || _p == 0) return;
            _y = FastExp(_g, _x, _p);
            TxtLog.AppendText($"Открытый ключ: ({_p}, {_g}, {_y})\n");
        }

        private void BtnSelectFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "All files (*.*)|*.*";
            if (dlg.ShowDialog() == true)
            {
                _sourceFilePath = dlg.FileName;
                TxtFilePath.Text = Path.GetFileName(_sourceFilePath);
                TxtLog.AppendText($"Файл выбран: {_sourceFilePath}\n");
            }
        }

        private void BtnEncrypt_Click(object sender, RoutedEventArgs e)
        {
            if (!PrepareEncryptionParams()) return;
            if (_g == 0)
            {
                MessageBox.Show("Выберите генератор g!");
                return;
            }
            if (string.IsNullOrEmpty(_sourceFilePath))
            {
                MessageBox.Show("Выберите файл!");
                return;
            }

            try
            {
                byte[] fileBytes = File.ReadAllBytes(_sourceFilePath);
                string outputPath = Path.Combine(Path.GetDirectoryName(_sourceFilePath),
                                                 "encrypted_" + Path.GetFileNameWithoutExtension(_sourceFilePath) + ".bin");

                using (FileStream fs = new FileStream(outputPath, FileMode.Create))
                using (BinaryWriter bw = new BinaryWriter(fs))
                {
                    TxtLog.AppendText($"\n--- ПРОЦЕСС ШИФРОВАНИЯ ---\n");
                    TxtLog.AppendText($"Чтение файла: {fileBytes.Length} байт.\n");
                    TxtLog.AppendText($"Запись в бинарный файл: {outputPath}\n\n");
                    TxtLog.AppendText("Дамп первых 20 пар (a, b) в 10-й СС:\n");

                    for (int i = 0; i < fileBytes.Length; i++)
                    {
                        BigInteger m = new BigInteger(fileBytes[i]);

                        // a = g^k mod p
                        BigInteger a = FastExp(_g, _k, _p);

                        // b = (y^k * m) mod p
                        BigInteger yk = FastExp(_y, _k, _p);
                        BigInteger bVal = (yk * m) % _p;

                        if (a > int.MaxValue || bVal > int.MaxValue)
                        {
                            MessageBox.Show($"Ошибка: Значения a или b превышают размер Int32 (2^31). Выберите меньшее простое число p.");
                            return;
                        }

                        bw.Write((int)a);
                        bw.Write((int)bVal);

                        if (i < 20)
                        {
                            TxtLog.AppendText($"Pair[{i}]: a={a}, b={bVal}\n");
                        }
                    }

                    TxtLog.AppendText($"\nШифрование завершено. Всего пар: {fileBytes.Length}\n");
                }

                StatusText.Text = "Файл зашифрован";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка шифрования: {ex.Message}");
            }
        }

        private void BtnDecrypt_Click(object sender, RoutedEventArgs e)
        {
            if (!PrepareEncryptionParams()) return;

            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Encrypted files (*.bin)|*.bin|All files (*.*)|*.*";
            dlg.Title = "Выберите зашифрованный бинарный файл (.bin)";

            if (dlg.ShowDialog() != true) return;

            try
            {
                List<byte> decryptedBytes = new List<byte>();

                using (FileStream fs = new FileStream(dlg.FileName, FileMode.Open))
                using (BinaryReader br = new BinaryReader(fs))
                {
                    TxtLog.AppendText($"\n--- ПРОЦЕСС ДЕШИФРОВАНИЯ ---\n");
                    TxtLog.AppendText($"Чтение файла: {dlg.FileName}\n");


                    while (br.BaseStream.Position + 8 <= br.BaseStream.Length)
                    {
                        int aInt = br.ReadInt32();
                        int bInt = br.ReadInt32();

                        BigInteger a = new BigInteger(aInt);
                        BigInteger bVal = new BigInteger(bInt);

                        // Дешифрование: m = b * a^(-x) mod p
                        BigInteger ax = FastExp(a, _x, _p);
                        BigInteger axInv = ModInverse(ax, _p);

                        BigInteger m = (bVal * axInv) % _p;

                        // Коррекция отрицательного остатка
                        if (m < 0) m += _p;

                        // Проверка диапазона
                        if (m > 255)
                        {
                            TxtLog.AppendText($"Предупреждение: Восстановленное значение {m} > 255. Данные могут быть повреждены.\n");
                        }

                        decryptedBytes.Add((byte)m);
                    }
                }

                if (decryptedBytes.Count == 0)
                {
                    MessageBox.Show("Файл пуст или имеет неверный формат.");
                    return;
                }

                string originalName = Path.GetFileNameWithoutExtension(dlg.FileName).Replace("encrypted_", "");
                string outputPath = Path.Combine(Path.GetDirectoryName(dlg.FileName), "decrypted_" + originalName);

                File.WriteAllBytes(outputPath, decryptedBytes.ToArray());

                TxtLog.AppendText($"Файл расшифрован. Результат: {outputPath}\n");
                TxtLog.AppendText($"Восстановлено байт: {decryptedBytes.Count}\n");

                StatusText.Text = "Дешифрование завершено";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка дешифрования: {ex.Message}\nУбедитесь, что выбран правильный .bin файл и ключи совпадают.");
            }
        }

        private bool PrepareEncryptionParams()
        {
            if (!BigInteger.TryParse(TxtP.Text, out _p)) { MessageBox.Show("Неверное p"); return false; }
            if (!BigInteger.TryParse(TxtX.Text, out _x)) { MessageBox.Show("Неверное x"); return false; }
            if (!BigInteger.TryParse(TxtK.Text, out _k)) { MessageBox.Show("Неверное k"); return false; }

            if (_g == 0) { MessageBox.Show("Выберите g!"); return false; }
            if (!IsPrime(_p)) { MessageBox.Show("p не простое"); return false; }
            if (_x <= 1 || _x >= _p - 1) { MessageBox.Show("x вне диапазона"); return false; }
            if (_k <= 1 || _k >= _p - 1) { MessageBox.Show("k вне диапазона"); return false; }
            if (BigInteger.GreatestCommonDivisor(_k, _p - 1) != 1)
            {
                MessageBox.Show("k и p-1 должны быть взаимно просты (НОД(k, p-1) = 1)");
                return false;
            }

            _y = FastExp(_g, _x, _p);
            return true;
        }

        #endregion
    }
}