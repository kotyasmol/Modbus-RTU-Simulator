using System;
using System.IO.Ports;
using System.Threading;
using System.Collections.Generic;

class Program
{
    class Slave
    {
        public byte Id { get; set; }
        public ushort StartRegister { get; set; }
        public ushort[] Registers { get; set; }

        public Slave(byte id, ushort start, ushort count)
        {
            Id = id;
            StartRegister = start;
            Registers = new ushort[count];
        }
    }

    static void Main()
    {
        Console.WriteLine("=== Modbus RTU Simulator ===");

        Console.Write("Введите COM-порт (например COM7): ");
        string portName = Console.ReadLine();

        using SerialPort port = new SerialPort(portName)
        {
            BaudRate = 9600,
            DataBits = 8,
            Parity = Parity.None,
            StopBits = StopBits.One,
            Handshake = Handshake.None
        };

        try
        {
            port.Open();
            Console.WriteLine($"COM-порт {portName} открыт.");

            // Настройка слейвов
            Console.Write("Сколько слейвов симулировать? ");
            int slaveCount = int.Parse(Console.ReadLine() ?? "1");

            var slaves = new List<Slave>();
            for (int i = 0; i < slaveCount; i++)
            {
                Console.WriteLine($"--- Настройка слейва #{i + 1} ---");
                Console.Write("ID слейва: ");
                byte id = byte.Parse(Console.ReadLine() ?? "1");
                Console.Write("Стартовый регистр: ");
                ushort start = ushort.Parse(Console.ReadLine() ?? "1000");
                Console.Write("Конечный регистр: ");
                ushort end = ushort.Parse(Console.ReadLine() ?? "1017");

                slaves.Add(new Slave(id, start, (ushort)(end - start + 1)));
            }

            Random rnd = new Random();

            Console.WriteLine("Эмулятор запущен. Ожидание запросов Modbus...");

            while (true)
            {
                if (port.BytesToRead > 0)
                {
                    int length = port.BytesToRead;
                    byte[] buffer = new byte[length];
                    port.Read(buffer, 0, length);

                    // Простая обработка: только Read Holding Registers (0x03) и Write Single Register (0x06)
                    if (length >= 8)
                    {
                        byte slaveId = buffer[0];
                        byte func = buffer[1];
                        ushort address = (ushort)(buffer[2] << 8 | buffer[3]);
                        ushort valueOrCount = (ushort)(buffer[4] << 8 | buffer[5]);

                        var slave = slaves.Find(s => s.Id == slaveId);
                        if (slave != null)
                        {
                            if (func == 0x03) // Read Holding Registers
                            {
                                int offset = address - slave.StartRegister;
                                if (offset >= 0 && offset + valueOrCount <= slave.Registers.Length)
                                {
                                    byte byteCount = (byte)(valueOrCount * 2);
                                    byte[] response = new byte[3 + byteCount + 2]; // Slave+Func+ByteCount+Data+CRC
                                    response[0] = slaveId;
                                    response[1] = 0x03;
                                    response[2] = byteCount;
                                    for (int i = 0; i < valueOrCount; i++)
                                    {
                                        ushort regVal = (ushort)rnd.Next(0, 65535);
                                        slave.Registers[offset + i] = regVal;
                                        response[3 + i * 2] = (byte)(regVal >> 8);
                                        response[4 + i * 2] = (byte)(regVal & 0xFF);
                                    }
                                    ushort crc = CalculateCRC(response, response.Length - 2);
                                    response[response.Length - 2] = (byte)(crc & 0xFF);
                                    response[response.Length - 1] = (byte)(crc >> 8);

                                    port.Write(response, 0, response.Length);
                                }
                            }
                            else if (func == 0x06) // Write Single Register
                            {
                                int offset = address - slave.StartRegister;
                                if (offset >= 0 && offset < slave.Registers.Length)
                                {
                                    slave.Registers[offset] = valueOrCount;
                                    // Эхо ответа
                                    port.Write(buffer, 0, 8);
                                }
                            }
                        }
                    }
                }

                Thread.Sleep(10);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
        }
    }

    // CRC16 для Modbus RTU
    static ushort CalculateCRC(byte[] data, int length)
    {
        ushort crc = 0xFFFF;
        for (int i = 0; i < length; i++)
        {
            crc ^= data[i];
            for (int j = 0; j < 8; j++)
            {
                if ((crc & 0x0001) != 0)
                    crc = (ushort)((crc >> 1) ^ 0xA001);
                else
                    crc >>= 1;
            }
        }
        return crc;
    }
}
