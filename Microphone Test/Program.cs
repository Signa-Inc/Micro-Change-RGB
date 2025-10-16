using System;
using System.Net.Http;
using System.Text;
using NAudio.Wave;
using Vosk;
using System.Text.Json;
using System.Threading.Tasks;

namespace VoiceToRgbControl
{
    class Program
    {
        // IP ESP32 в вашей Wi-Fi сети
        const string EspIp = "192.168.0.115"; // <- Укажи IP, который ESP32 получил после подключения к Wi-Fi

        static HttpClient httpClient = new HttpClient();

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            // Путь к распакованной модели Vosk
            const string modelPath = @"F:\VS projects\Micro-Change-RGB\vosk-model-small-ru-0.22";

            if (!System.IO.Directory.Exists(modelPath))
            {
                Console.WriteLine($"❌ Модель не найдена по пути: {modelPath}");
                return;
            }

            // Инициализация Vosk
            Vosk.Vosk.SetLogLevel(-1);
            Model model = new Model(modelPath);
            var recognizer = new VoskRecognizer(model, 16000.0f);

            // Настройка микрофона
            using var waveIn = new WaveInEvent
            {
                DeviceNumber = 0,
                WaveFormat = new WaveFormat(16000, 1)
            };

            waveIn.DataAvailable += (s, a) =>
            {
                if (recognizer.AcceptWaveform(a.Buffer, a.BytesRecorded))
                {
                    var resultJson = recognizer.Result();
                    string text = ExtractText(resultJson);
                    if (!string.IsNullOrEmpty(text))
                    {
                        Console.WriteLine($"🗣 Распознано: {text}");
                        ProcessCommand(text).Wait(); // Отправляем команду на ESP32
                    }
                }
            };

            Console.WriteLine("🎙 Говори! (нажмите Enter чтобы остановить)");
            waveIn.StartRecording();
            Console.ReadLine();
            waveIn.StopRecording();
            Console.WriteLine("🛑 Распознавание завершено.");
        }

        // Извлечение текста из JSON результата Vosk
        static string ExtractText(string json)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("text", out var t))
                    return t.GetString();
            }
            catch { }
            return "";
        }

        // Преобразование распознанного текста в цвет и отправка на ESP32
        static async Task ProcessCommand(string text)
        {
            string color = null;
            text = text.ToLower();

            if (text.Contains("красный")) color = "red";
            else if (text.Contains("зелёный") || text.Contains("зеленый")) color = "green";
            else if (text.Contains("синий")) color = "blue";
            else if (text.Contains("жёлтый") || text.Contains("желтый")) color = "yellow";
            else if (text.Contains("магентa") || text.Contains("пурпурный")) color = "magenta";
            else if (text.Contains("циан") || text.Contains("голубой")) color = "cyan";
            else if (text.Contains("белый")) color = "white";
            else if (text.Contains("выключи") || text.Contains("выкл")) color = "off";
            else if (text.Contains("собака")) color = "white";

            if (color != null)
            {
                await SendColorToESP(color);
            }
            else
            {
                Console.WriteLine($"⚠ Неизвестная команда: {text}");
            }
        }

        // Отправка команды на ESP32
        static async Task SendColorToESP(string color)
        {
            try
            {
                string url = $"http://{EspIp}/setcolor?color={color}";
                var response = await httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    string respText = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"✅ ESP32 ответил: {respText}");
                }
                else
                {
                    Console.WriteLine($"❌ Ошибка HTTP: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка отправки: {ex.Message}");
            }
        }
    }
}
