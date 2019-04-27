using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;                                            // Работа с файлами.
using System.Collections;                                   // Работа с динамическим массивом (ArrayList).


namespace musicalСard
{

    public partial class MIDItoК1986ВЕ92QI : Form
    {
        public MIDItoК1986ВЕ92QI()
        {
            InitializeComponent();
        }

   

        // Назначение: Хранить параметры заголовка MIDI файла. 
        // Применение: Структура создается при первом чтении MIDI файла.
        public struct MIDIheaderStruct
        {
            public string nameSection;                                                                // Имя раздела. Должно быть "MThd".
            public UInt32 lengthSection;                                                              // Длинна блока, 4 байта. Должно быть 0x6;
            public UInt16 mode;                                                                       // Режим MIDI файла: 0, 1 или 2. 
            public UInt16 channels;                                                                   // Количество каналов. 
            public UInt16 settingTime;                                                                // Параметры тактирования.
        }

        // Назначение: Хранить блок с событиями MIDI блока.
        // Применение: Создается перед чтением очередного блока MIDI файла.
        public struct MIDIMTrkStruct
        {
            public string nameSection; // Имя раздела. Должно быть "MTrk".
            public UInt32 lengthSection; // Длинна блока, 4 байта.
            public ArrayList arrayNoteStruct; // Динамический массив с нотами и их изменениями.
        }

        // Назначение: хранить события нажатия/отпускания клавиши или смены ее громкости.
        public struct noteStruct
        {
            public byte     roomNotes;                    // Номер ноты.
            public UInt32   noteTime;                     // Длительность ноты время обсалютное. 
            public byte     dynamicsNote;                 // Динамика взятия/отпускания ноты.
            public byte     channelNote;                  // Канал ноты.
            public bool     flagNote;                     // Взятие ноты (true) или отпускание ноты (false).    
        }

        // Назначение: разбор главной структуры MIDI файла.
        // Параметры: Открытый FileStream поток.
        // Возвращаемой значение - заполненная структура типа MIDIheaderStruct.
        public MIDIheaderStruct CopyHeaderOfMIDIFile(MIDIReaderFile MIDIFile)
        {
            MIDIheaderStruct ST = new MIDIheaderStruct();                   // Создаем пустую структуру заголовка файла.
            ST.nameSection      = MIDIFile.ReadStringOf4byte();             // Копируем имя раздела. 
            ST.lengthSection    = MIDIFile.ReadUInt32BigEndian();           // Считываем 4 байта длины блока. Должно в итоге быть 0x6
            ST.mode             = MIDIFile.ReadUInt16BigEndian();           // Считываем 2 байта режима MIDI. Должно быть 0, 1 или 2.
            ST.channels         = MIDIFile.ReadUInt16BigEndian();           // Считываем 2 байта количество каналов в MIDI файле. 
            ST.settingTime      = MIDIFile.ReadUInt16BigEndian();           // Считываем 2 байта параметров тактирования.
            return ST;                                                      // Возвращаем заполненную структуру.
        }
        
        // Назначение: копирование блока MTrk (блок с событиями) из MIDI файла.
        // Параметры: поток для чтения MIDI файла.
        // Возвращает: структуру блока с массивом структур событий.
        public MIDIMTrkStruct CopyMIDIMTrkSection(MIDIReaderFile MIDIFile)
        {
            MIDIMTrkStruct ST = new MIDIMTrkStruct(); // Создаем пустую структуру блока MIDI файла. 
            ST.arrayNoteStruct = new ArrayList(); // Создаем в структуре блока динамический массив структур событий клавиш.
            noteStruct bufferSTNote = new noteStruct(); // Создаем запись о новой ноте (буферная структура, будем класть ее в arrayNoteStruct).
            ST.nameSection      = MIDIFile.ReadStringOf4byte(); // Копируем имя раздела. 
            ST.lengthSection    = MIDIFile.ReadUInt32BigEndian(); // 4 байта длинны всего блока.
            UInt32 LoopIndex    = ST.lengthSection; // Копируем колличество оставшихся ячеек. Будем считывать события, пока счетчик не будет = 0.
            UInt32 realTime = 0; // Реальное время внутри блока.
            while (LoopIndex != 0) // Пока не считаем все события.
            {
                // Время описывается плавающим числом байт. Конечный байт не имеет 8-го разрядка справа (самого старшего).
                byte loopСount = 0; // Колличество считанных байт.
                byte buffer; // Сюда кладем считанное значение.
                UInt32 bufferTime = 0; // Считанное время помещаем сюда.                              
                do {
                    buffer = MIDIFile.ReadByte(); // Читаем значение.
                    loopСount++; // Показываем, что считали байт.
                    bufferTime <<=  7; // Сдвигаем на 7 байт влево существующее значенеи времени (Т.к. 1 старший байт не используется).
                    bufferTime |= (byte)(buffer & (0x7F)); // На сдвинутый участок накладываем существующее время.
                } while ((buffer & (1<<7)) != 0); // Выходим, как только прочитаем последний байт времени (старший бит = 0).
                realTime += bufferTime; // Получаем реальное время.

                buffer = MIDIFile.ReadByte(); loopСount++; // Считываем статус-байт, показываем, что считали байт. 
                // Если у нас мета-события, то...
                if (buffer == 0xFF)                                         
                {
                    buffer = MIDIFile.ReadByte(); // Считываем номер мета-события.
                    buffer = MIDIFile.ReadByte(); // Считываем длину.
                    loopСount+=2;
                    for (int loop = 0; loop < buffer; loop++)
                        MIDIFile.ReadByte();
                    LoopIndex = LoopIndex - loopСount - buffer; // Отнимаем от счетчика длинну считанного.   
                } 
  
                // Если не мета-событие, то смотрим, является ли событие событием первого уровня.
                else switch ((byte)buffer & 0xF0) // Смотрим по старшым 4-м байтам.
                {
                    // Перебираем события первого уровня.
                   
                    case 0x80: // Снять клавишу.
                        bufferSTNote.channelNote = (byte)(buffer & 0x0F); // Копируем номер канала.
                        bufferSTNote.flagNote = false; // Мы отпускаем клавишу.
                        bufferSTNote.roomNotes = MIDIFile.ReadByte(); // Копируем номер ноты.
                        bufferSTNote.dynamicsNote = MIDIFile.ReadByte(); // Копируем динамику ноты.
                        bufferSTNote.noteTime = realTime; // Присваеваем реальное время ноты.
                        ST.arrayNoteStruct.Add(bufferSTNote); // Сохраняем новую структуру.
                        LoopIndex = LoopIndex - loopСount - 2; // Отнимаем прочитанное. 
                        break;
                    case 0x90:   // Нажать клавишу.
                        bufferSTNote.channelNote = (byte)(buffer & 0x0F); // Копируем номер канала.
                        bufferSTNote.flagNote = true; // Мы нажимаем.
                        bufferSTNote.roomNotes = MIDIFile.ReadByte(); // Копируем номер ноты.
                        bufferSTNote.dynamicsNote = MIDIFile.ReadByte(); // Копируем динамику ноты.
                        bufferSTNote.noteTime = realTime; // Присваеваем реальное время ноты.
                        ST.arrayNoteStruct.Add(bufferSTNote); // Сохраняем новую структуру.
                        LoopIndex = LoopIndex - loopСount - 2; // Отнимаем прочитанное. 
                        break;
                    case 0xA0:  // Сменить силу нажатия клавишы. 
                        bufferSTNote.channelNote = (byte)(buffer & 0x0F); // Копируем номер канала.
                        bufferSTNote.flagNote = true; // Мы нажимаем.
                        bufferSTNote.roomNotes = MIDIFile.ReadByte(); // Копируем номер ноты.
                        bufferSTNote.dynamicsNote = MIDIFile.ReadByte(); // Копируем НОВУЮ динамику ноты.
                        bufferSTNote.noteTime = realTime; // Присваеваем реальное время ноты.
                        ST.arrayNoteStruct.Add(bufferSTNote); // Сохраняем новую структуру.
                        LoopIndex = LoopIndex - loopСount - 2; // Отнимаем прочитанное.     
                        break;
                    // Если 2-х байтовая комманда.
                    case 0xB0:  byte buffer2level = MIDIFile.ReadByte(); // Читаем саму команду.
                                switch (buffer2level) // Смотрим команды второго уровня.
                                {
                                    default: // Для определения новых комманд (не описаных).
                                        MIDIFile.ReadByte(); // Считываем параметр какой-то неизвестной функции.
                                        LoopIndex = LoopIndex - loopСount - 2; // Отнимаем прочитанное. 
                                        break;                                              
                                }
                                break;
                   
                    // В случае попадания их просто нужно считать.
                    case 0xC0:   // Просто считываем байт номера.
                        MIDIFile.ReadByte(); // Считываем номер программы.
                        LoopIndex = LoopIndex - loopСount - 1; // Отнимаем прочитанное. 
                        break;
                   
                    case 0xD0:   // Сила канала.
                        MIDIFile.ReadByte(); // Считываем номер программы.
                        LoopIndex = LoopIndex - loopСount - 1; // Отнимаем прочитанное. 
                        break;
                   
                    case 0xE0:  // Вращения звуковысотного колеса.
                        MIDIFile.ReadBytes(2); // Считываем номер программы.
                        LoopIndex = LoopIndex - loopСount - 2; // Отнимаем прочитанное. 
                        break;
                }
            }
            return ST; // Возвращаем заполненную структуру.
        }
        



        // Назначение: создавать список: нота/длительность.
        // Параметры: массив структур блоков, каждый из которых содержит массив структур событий; количество блоков.
        public ArrayList СreateNotesArray(MIDIMTrkStruct[] arrayST, int arrayCount)
        {
            ArrayList arrayChannelNote = new ArrayList(); // Массив каналов.

            for (int indexBlock = 0; indexBlock < arrayCount; indexBlock++) // Проходим по всем блокам MIDI.
            {
                for (int eventArray = 0; eventArray < arrayST[indexBlock].arrayNoteStruct.Count; eventArray++) // Пробегаемся по всем событиям массива каждого канала.
                {
                    noteStruct bufferNoteST = (noteStruct)arrayST[indexBlock].arrayNoteStruct[eventArray]; // Достаем событие ноты.
                    if (bufferNoteST.flagNote == true) // Если нажимают ноту.
                    {
                        byte indexChennelNoteWrite = 0;
                        while (true) // Перебераем каналы для записи.
                        {
                            if (indexChennelNoteWrite<arrayChannelNote.Count) // Если мы еще не просмотрели все существующие каналы.
                            {
                                channelNote bufferChannel = (channelNote)arrayChannelNote[indexChennelNoteWrite]; // Достаем канал с выбранным номером.

                                if (bufferChannel.ToWriteaNewNote(bufferNoteST.roomNotes, bufferNoteST.noteTime) == true) break; // Если запись проша удачно - выходим.
                            }
                            else // Если свободного канала не найдено - создать новый и кинуть в него все.
                            {
                                channelNote noteNambeChannelBuffer = new channelNote(); // Канал с реальным временем предыдущего.
                                noteNambeChannelBuffer.ToWriteaNewNote(bufferNoteST.roomNotes, bufferNoteST.noteTime);// Если запись проша удачно - выходим.
                                arrayChannelNote.Add(noteNambeChannelBuffer); // Добавляем канал в массив каналов.
                                break;  // Наверняка выходим.
                            }
                            indexChennelNoteWrite++; // Если не удалось записать - следующий канал.
                        }
                    }
                    else // Если ноту наоборот отпускают.
                    {
                        byte indexChennelNoteWrite = 0;
                        while (true) // Перебераем каналы для записи.
                        {
                                channelNote bufferChannel = (channelNote)arrayChannelNote[indexChennelNoteWrite]; // Достаем канал с выбранным номером.
                                if (bufferChannel.EntryEndNotes(bufferNoteST.roomNotes, bufferNoteST.noteTime) == true) break;// Если запись проша удачно - выходим.
                                indexChennelNoteWrite++; // Если не удалось записать - следующий канал.
                        }
                    }
                }
            }
            return arrayChannelNote;
        }

        // Вывод массивов каналов в richTextBox1.
        public void outData(ArrayList Data)
        {
            for (int loop = 0; loop<Data.Count; loop++) // Идем по всем каналам.
            {
                channelNote buffer = (channelNote)Data[loop]; // Получаем ссылку на канал.
                // Проходимся по всем нотам канала.
                richTextBox1.Text += "uint16_t channel" + loop.ToString() + "[" + buffer.arrayNoteChannel.Count.ToString() + "][2] = {";
                for (int loop1 = 0; loop1 < buffer.arrayNoteChannel.Count; loop1++)
                {
                    channelNote.noteInChannelNote DataD = (channelNote.noteInChannelNote)buffer.arrayNoteChannel[loop1];
                    richTextBox1.Text += DataD.roomNotes.ToString() + "," + DataD.noteTime.ToString();
                    if (loop1 != (buffer.arrayNoteChannel.Count - 1)) richTextBox1.Text += ", \t";
                }
                richTextBox1.Text += "};\n\n";
            }
        }

        // Назначение: Открытие файла для чтения. 
        // Параметры: путь к файлу.
        // Возвращаемое значение: успешность операции. true - успешно, false - нет.
        public bool openMIDIFile(string pathToFile)
        {
            FileStream fileStream = new FileStream(pathToFile, FileMode.Open, FileAccess.Read);  // Открываем файл только для чтения.
            MIDIReaderFile MIDIFile = new MIDIReaderFile(fileStream);                            // Собственный поток для работы с MIDI файлом со спец. функциями. На основе байтового потока открытого файла.
            MIDIheaderStruct HeaderMIDIStruct = CopyHeaderOfMIDIFile(MIDIFile);                  // Считываем заголовок.
            MIDIMTrkStruct[] MTrkStruct = new MIDIMTrkStruct[HeaderMIDIStruct.channels];         // Определяем массив для MTrkStruct.
            richTextBox1.Text += "Количество блоков: " + HeaderMIDIStruct.channels.ToString() + "\n"; // Количество каналов.
            richTextBox1.Text += "Параметры времени: " + HeaderMIDIStruct.settingTime.ToString() + "\n";
            richTextBox1.Text += "Формат MIDI: " + HeaderMIDIStruct.mode.ToString() + "\n";     
            for (int loop = 0; loop<HeaderMIDIStruct.channels; loop++)
                MTrkStruct[loop] = CopyMIDIMTrkSection(MIDIFile);                                // Читаем блоки MIDI файла.
            outData(СreateNotesArray(MTrkStruct, HeaderMIDIStruct.channels));                    // Получаем список нота/длительность.
            return true;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            richTextBox1.Clear();
            if (openFileDialogMIDI.ShowDialog() == DialogResult.OK)         // Если диалоговое окно нормально открылось.
            {
                openMIDIFile(openFileDialogMIDI.FileName);                  // Открываем файл для чтения.
            }
        }
    }

    // Класс для работы с файловым потоком файла MIDI.
    public class MIDIReaderFile 
    {
        public BinaryReader BinaryReaderMIDIFile;   // Создаем поток. На его основе будем работать с MIDI файлом.
        public MIDIReaderFile(Stream input) // В конструкторе инициализируем байтовый поток на основе открытого потока.
        {
            BinaryReaderMIDIFile = new BinaryReader(input); // Открываем поток для чтения по байтам на основе открытого потока файла.
        }

        public UInt32 ReadUInt32BigEndian() // Считываем 4 байта в формате "от старшего к младшему" и располагаем их в переменной.
        {
            UInt32 bufferData = 0;  // Начальное значени = 0.
            for (int IndexByte = 3; IndexByte >= 0; IndexByte--)    // Счетчик от старшего к младшему.
                bufferData |= (UInt32)((UInt32)BinaryReaderMIDIFile.ReadByte()) << 8 * IndexByte;   // Располагаем значения. 
            return bufferData;
        }

        public UInt16 ReadUInt16BigEndian() // Считываем 2 байта в формате "от старшего к младшему" и располагаем их в переменной.
        {
            UInt16 bufferData = 0;  // Начальное значени = 0.
            for (int IndexByte = 1; IndexByte >= 0; IndexByte--)    // Счетчик от старшего к младшему.
                bufferData |= (UInt16)((UInt16)BinaryReaderMIDIFile.ReadByte() << 8 * IndexByte);   // Располагаем значения. 
            return bufferData;
        }

        public string ReadStringOf4byte()   // Получаем из файла строку в 4 элемента.
        {
            return Encoding.Default.GetString(BinaryReaderMIDIFile.ReadBytes(4));   // Достаем 4 байта и преобразовываем их в стоку из 4-х символов.
        }

        public byte ReadByte()  // Считываем 1 байт.
        {
            return BinaryReaderMIDIFile.ReadByte();
        }

        public byte[] ReadBytes(int count)  // Считываем count байт.
        {
            return BinaryReaderMIDIFile.ReadBytes(count);
        }
    }

            // Назначение: хранить в себе массивы нот и пауз для полифонии в одну ноту.
    public class channelNote
    {
        // Назначение: хранить уже отыгранные ноты в однополифоническом канале в структуре channelNote.
        public struct noteInChannelNote
        {
            public byte roomNotes; // Номер ноты или пауза.
            public UInt32 noteTime; // Длительность ноты.
        }

            bool flag; // Играет ли в данный момент кака-нибудь нота.
            byte notesRealTime; // Номер ноты, которая играет на канале в настоящее время. false - свободно. true - занят.
            UInt32 time; // Время начала ноты/паузы.
            public ArrayList arrayNoteChannel; // Уже отыгранные ноты на канале.

            public channelNote()
            {
                arrayNoteChannel = new ArrayList();
            }
            private void WriteToArrayNewNote(UInt32 endTime) // Записываем ноту в массив Параметр - конечное время. От него отнимается последнее записанное.
            {
                noteInChannelNote bufferNewNote = new noteInChannelNote();
                bufferNewNote.noteTime = (UInt32)(endTime - time); // Время действия ноты.
                bufferNewNote.roomNotes = notesRealTime; // Номер ноты (паузы возможно).
                arrayNoteChannel.Add(bufferNewNote); // Записываем в массив.
            }

            public bool ToWriteaNewNote(byte nambe, UInt32 timeNew) // Фиксируем начало ноты.
            {
                if (flag == false) // Если канал в данный момент не воспроизводит ноту, то двигаемся дальше. Иначе отправляем ошибку, канал занят.
                {
                    if ((timeNew - time) != 0) // Если пауза не равна 0, то нужно записать, что была "нота (код паузы)" длительностью в паузу.
                    {
                        if (timeNew < time)
                        {
                            time = 1;
                        }
                        notesRealTime = 131; // 0xFF - пауза.
                        WriteToArrayNewNote(timeNew); // Записываем паузу.
                    }
                    
                    // Далее просто записываем ноту во временный буффер.
                    time = timeNew;
                    notesRealTime = nambe;
                    flag = true; // Канал теперь занят.
                    return true; // Канал занят, успешно выходим.
                }
                else return false; // Канал занят.
            }

            public bool EntryEndNotes (byte nambe, UInt32 timeNew) // Запись окончания ноты на канале.
            {
                if (flag == true) // Если канал в данный момент воспроизводил звук.
                {
                    if (notesRealTime == nambe) // Если пытаются завершить ноту, которая играет именно на этом канале.
                    {
                        WriteToArrayNewNote(timeNew); // Записываем ноту (номер ноты уже в буфере, передаем только время окончания).
                        time = timeNew; // Это же время сохраняем в буфере канала для проверки надобности задержки при следующей записи ноты.
                        flag = false; // Канал снова доступен для записи.
                        return true;
                    }
                    else return false; // Иначе обращение было не к этому каналу.
                }
                else return false; // Иначе автоматически не то.
            }
    }
}
