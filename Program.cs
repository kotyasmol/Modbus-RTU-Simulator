using System;
using System.IO.Ports;
using System.Threading;

class Program
{
    static ushort[] registers = new ushort[13];
    static Random rnd = new Random();

    static void Main()
    {
        Console.WriteLine("=== Modbus RTU Simulator ===");

        Console.Write("Введите COM-порт: ");
        string portName = Console.ReadLine();

        using SerialPort port = new SerialPort(portName)
        {
            BaudRate = 9600,
            DataBits = 8,
            Parity = Parity.None,
            StopBits = StopBits.One
        };

        InitializeRegisters();

        new Thread(UpdateRandomRegisters) { IsBackground = true }.Start();

        port.Open();
        Console.WriteLine("Порт открыт");

        while (true)
        {
            if (port.BytesToRead > 0)
            {
                byte[] request = new byte[8];
                port.Read(request, 0, 8);

                byte slaveId = request[0];
                byte func = request[1];
                ushort address = (ushort)(request[2] << 8 | request[3]);
                ushort value = (ushort)(request[4] << 8 | request[5]);

                if (func == 0x03)
                {
                    ushort count = value;
                    SendReadResponse(port, slaveId, address, count);
                }
                else if (func == 0x06)
                {
                    WriteRegister(address, value);
                    port.Write(request, 0, 8);
                }
            }

            Thread.Sleep(5);
        }
    }

    static void InitializeRegisters()
    {
        // регистр 0 — "hello world" packed ASCII (h=0x68 e=0x65)
        registers[0] = (ushort)((byte)'h' << 8 | (byte)'e');

        // 6-10 фиксированные
        registers[6] = 111;
        registers[7] = 222;
        registers[8] = 333;
        registers[9] = 444;
        registers[10] = 555;

        registers[11] = 0;
        registers[12] = 0;
    }

    static void UpdateRandomRegisters()
    {
        while (true)
        {
            for (int i = 1; i <= 5; i++)
            {
                registers[i] = (ushort)rnd.Next(2000, 6000);
            }

            Thread.Sleep(1000);
        }
    }

    static void WriteRegister(int address, ushort value)
    {
        if (address == 11)
        {
            registers[11] = (ushort)(value == 0 ? 0 : 1);
        }
        else if (address == 12)
        {
            registers[12] = value;
        }
    }

    static void SendReadResponse(SerialPort port, byte slaveId, ushort start, ushort count)
    {
        byte byteCount = (byte)(count * 2);
        byte[] response = new byte[3 + byteCount + 2];

        response[0] = slaveId;
        response[1] = 0x03;
        response[2] = byteCount;

        for (int i = 0; i < count; i++)
        {
            ushort val = registers[start + i];
            response[3 + i * 2] = (byte)(val >> 8);
            response[4 + i * 2] = (byte)(val & 0xFF);
        }

        ushort crc = CalculateCRC(response, response.Length - 2);
        response[^2] = (byte)(crc & 0xFF);
        response[^1] = (byte)(crc >> 8);

        port.Write(response, 0, response.Length);
    }

    static ushort CalculateCRC(byte[] data, int length)
    {
        ushort crc = 0xFFFF;

        for (int i = 0; i < length; i++)
        {
            crc ^= data[i];

            for (int j = 0; j < 8; j++)
            {
                if ((crc & 1) != 0)
                    crc = (ushort)((crc >> 1) ^ 0xA001);
                else
                    crc >>= 1;
            }
        }

        return crc;
    }
}