using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Data;
using System.Net;
using System.Xml;
using System.Threading;
using Microsoft.Office.Interop.Excel;
using System.Runtime.InteropServices;
using System.Diagnostics;


namespace PERSReport.Utilities
{
    class ExcelIntegration
    {
        Application _excelApp;
        System.Data.DataTable _valuesTable;

        public ExcelIntegration()
        {
            _excelApp = null;
            _valuesTable = new System.Data.DataTable("excel");
        }

        ~ExcelIntegration()
        {
            if (_excelApp != null)
            {
                _excelApp.Quit();
            }
            _valuesTable.Clear();
        }

        public void Close()
        {
            if (_excelApp != null)
            {
                _excelApp.Quit();
            }
            _excelApp = null;
            _valuesTable.Clear();
        }


        public bool GetWorksheetFromExcel(string filePath, string worksheet, ref DataSet excelRecords, string tableName)
        {
            Debug.Assert(excelRecords != null);
            Debug.Assert(!string.IsNullOrEmpty(tableName));

            _valuesTable.Clear();

            if (_excelApp == null)
                _excelApp = new Application();

            GetSheet(filePath, worksheet);

            if (_valuesTable.Rows.Count == 0)
                return false;

            excelRecords.Tables.Add(_valuesTable.Copy());
            excelRecords.Tables["excel"].TableName = tableName;

            return true;
        }


        private void GetSheet(string fileName, string sheetName)
        {
            Debug.Assert(_excelApp != null);

            _valuesTable.Clear();
            _valuesTable.Columns.Clear();

            //
            // This mess of code opens an Excel workbook. I don't know what all
            // those arguments do, but they can be changed to influence behavior.
            //
            Workbook workBook = _excelApp.Workbooks.Open(fileName,
                Type.Missing, Type.Missing, Type.Missing, Type.Missing,
                Type.Missing, Type.Missing, Type.Missing, Type.Missing,
                Type.Missing, Type.Missing, Type.Missing, Type.Missing,
                Type.Missing, Type.Missing);

            //
            // Pass the workbook to a separate function. This new function
            // will iterate through the worksheets in the workbook.
            //
            ExcelScanWorksheet(workBook, sheetName);

            //
            // Clean up.
            //
            workBook.Close(false, fileName, null);
            Marshal.ReleaseComObject(workBook);
            return;
        }


        private void ExcelScanWorksheet(Workbook workBookIn, string sheetName)
        {
            //
            // Get sheet Count and store the number of sheets.
            //
            int numSheets = workBookIn.Sheets.Count;

            //
            // Iterate through the sheets. They are indexed starting at 1.
            //
            for (int sheetNum = 1; sheetNum < numSheets + 1; sheetNum++)
            {
                Worksheet sheet = (Worksheet)workBookIn.Sheets[sheetNum];

                // Ignore any sheet not specified as an update sheet.
                if (G.IsDiff(sheet.Name, sheetName))
                    continue;

                //
                // Take the used range of the sheet. Finally, get an object array of all
                // of the cells in the sheet (their values). 
                //
                Range excelRange = sheet.UsedRange;
                object[,] valueArray = (object[,])excelRange.get_Value(XlRangeValueDataType.xlRangeValueDefault);

                // 
                // Convert the 2-D valueArray to a Table for ease of use.   First Row is Header.
                //
                for (int i = 1, j = 1; j <= valueArray.GetLength(1); j++)
                {
                    // the first row is contains column headers, quit on first empty column name.
                    if (string.IsNullOrEmpty(valueArray[i, j] as string))
                        break;
                    else
                        _valuesTable.Columns.Add(valueArray[i, j] as string, typeof(string));
                }

                //
                // Convert the remaining rows to Table format
                //
                for (int i = 2; i <= valueArray.GetLength(0); i++)
                {
                    string[] row = new string[_valuesTable.Columns.Count];
                    for (int j = 1; j <= _valuesTable.Columns.Count; j++)
                    {
                        if (valueArray[i, j] == null)
                        {
                            row[j - 1] = string.Empty;
                            continue;
                        }

                        //Debug.WriteLine("Type={0}", valueArray[i, j].GetType());

                        if (valueArray[i, j].GetType() == typeof(System.Double))
                        {
                            row[j - 1] = ((double)valueArray[i, j]).ToString("G");      // Excel presents all 'numbers' as type double.  argh!
                        }
                        else if (valueArray[i, j].GetType() == typeof(System.DateTime))
                        {
                            row[j - 1] = ((DateTime)valueArray[i, j]).ToString("G");    // parse DateTime into day format
                        }
                        else if (!string.IsNullOrEmpty(valueArray[i, j] as string))
                        {
                            row[j - 1] = valueArray[i, j] as string;
                        }
                        else
                            row[j - 1] = string.Empty;

                        // TODO : May need to deal with more excel types individually (e.g. dates).
                        //Debug.WriteLine($"Type={valueArray[i, j].GetType()} : Value={ row[j - 1] }");

                    }
                    _valuesTable.Rows.Add(row);
                }

                break;   // ignore any further sheets
            }
            return;
        }


        public void SaveDataSetToExcelFile(System.Data.DataSet ds, string filename)
        {
            Debug.Assert(ds != null);

            if (_excelApp == null)
                _excelApp = new Application();

            Debug.Assert(_excelApp != null);

            Workbook wb = _excelApp.Workbooks.Add(1);
            Worksheet ws = (Worksheet)wb.Worksheets[1];

            int skipFirst = 1;
            foreach (System.Data.DataTable dt in ds.Tables)
            {
                if (skipFirst > 1)
                {
                    wb.Sheets.Add(After: wb.Sheets[wb.Sheets.Count]);
                    ws = (Worksheet)wb.Worksheets[wb.Sheets.Count];
                }
                else
                {
                    skipFirst++;
                }

                ws.Name = dt.TableName;

                // utilize a temporary 2D array to prepare our table data for export.
                // add a row for the column titles plus enough rows to fill with data elements.
                string[,] data = new string[dt.Rows.Count + 1, dt.Columns.Count];

                // export column headers.  Excel indexes start at 1, our table index starts at 0.
                for (int colNdx = 0; colNdx < dt.Columns.Count; colNdx++)
                {
                    //ws.Cells[1, colNdx + 1] = dt.Columns[colNdx].ColumnName;
                    data[0, colNdx] = dt.Columns[colNdx].ColumnName;
                }

                // export data rows.
                for (int rowNdx = 0; rowNdx < dt.Rows.Count; rowNdx++)
                {
                    for (int colNdx = 0; colNdx < dt.Columns.Count; colNdx++)
                    {
                        if ((dt.Columns[colNdx].ColumnName.Contains("Rate")) || (dt.Columns[colNdx].ColumnName.Contains("Ratio")))
                            data[rowNdx + 1, colNdx] = GetStringFromRate(dt.Rows[rowNdx][colNdx]);
                        else
                            data[rowNdx + 1, colNdx] = GetString(dt.Rows[rowNdx][colNdx]);
                    }
                }

                // export as a range for COM interop efficiency.
                var startCell = (Range)ws.Cells[1, 1];
                var endCell = (Range)ws.Cells[dt.Rows.Count + 1, dt.Columns.Count];
                var writeRange = ws.Range[startCell, endCell];
                writeRange.Value2 = data;
            }

            // delete the default Sheet 1 - which is empty anyway.
            //_excelApp.DisplayAlerts = false;
            //_excelApp.EnableEvents = false;
            //((Worksheet)wb.Worksheets[1]).Delete();
            //_excelApp.DisplayAlerts = true;
            //_excelApp.EnableEvents = true;

            wb.SaveAs(filename, Type.Missing, Type.Missing, Type.Missing,
                Type.Missing, Type.Missing, XlSaveAsAccessMode.xlNoChange,
                Type.Missing, Type.Missing, Type.Missing,
                Type.Missing, Type.Missing);

            wb.Close(false, Type.Missing, Type.Missing);
            Marshal.ReleaseComObject(wb);

            return;
        }


        public void SaveTableToExcelFile(System.Data.DataTable dt, string filename)
        {
            Debug.Assert(dt != null);

            if (_excelApp == null)
                _excelApp = new Application();

            Debug.Assert(_excelApp != null);

            Workbook wb = _excelApp.Workbooks.Add(1);
            Worksheet ws = (Worksheet)wb.Worksheets[1];

            // TODO : Add concurrency support eventually...

            // utilize a temporary 2D array to prepare our table data for export.
            // add a row for the column titles plus enough rows to fill with data elements.
            string[,] data = new string[dt.Rows.Count + 1, dt.Columns.Count];

            // export column headers.  Excel indexes start at 1, our table index starts at 0.
            for (int colNdx = 0; colNdx < dt.Columns.Count; colNdx++)
            {
                //ws.Cells[1, colNdx + 1] = dt.Columns[colNdx].ColumnName;
                data[0, colNdx] = dt.Columns[colNdx].ColumnName;
            }

            // export data rows.
            for (int rowNdx = 0; rowNdx < dt.Rows.Count; rowNdx++)
            {
                for (int colNdx = 0; colNdx < dt.Columns.Count; colNdx++)
                {
                    if ((dt.Columns[colNdx].ColumnName.Contains("Rate")) || (dt.Columns[colNdx].ColumnName.Contains("Ratio")))
                        data[rowNdx + 1, colNdx] = GetStringFromRate(dt.Rows[rowNdx][colNdx]);
                    else
                        data[rowNdx + 1, colNdx] = GetString(dt.Rows[rowNdx][colNdx]);
                }
            }

            // export as a range for COM interop efficiency.
            var startCell = (Range)ws.Cells[1, 1];
            var endCell = (Range)ws.Cells[dt.Rows.Count + 1, dt.Columns.Count];
            var writeRange = ws.Range[startCell, endCell];
            writeRange.Value2 = data;

            wb.SaveAs(filename, Type.Missing, Type.Missing, Type.Missing,
                Type.Missing, Type.Missing, XlSaveAsAccessMode.xlNoChange,
                Type.Missing, Type.Missing, Type.Missing,
                Type.Missing, Type.Missing);

            wb.Close(false, Type.Missing, Type.Missing);
            Marshal.ReleaseComObject(wb);

            return;
        }

        private string GetString(object o)
        {
            if (o == null)
                return "";
            if (o is decimal)
                return G.R2((decimal)o);        // round to the 2nd decimal place
            return o.ToString();
        }

        private string GetStringFromRate(object o)
        {
            if (o == null)
                return "";
            if (o is decimal)
                return G.R4((decimal)o);    // round to the 4th decimal place
            return o.ToString();
        }
    }
}
