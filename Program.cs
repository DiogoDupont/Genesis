using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Genesis
{
    internal static class Program
    {
        /// <summary>
        /// Genesis.exe [projeto.genesis] [--pagina=tags|maquinas|partidas|sensores|pids|modbus|gerar|config]
        /// (abrir um projeto pelo Explorer passa o caminho como primeiro argumento)
        /// </summary>
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // erro inesperado na tela (ex.: dentro da grade do Windows): registra no log e segue,
            // em vez de abrir a janela "Ocorreu uma excecao sem tratamento"
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate (object s, System.Threading.ThreadExceptionEventArgs e)
            {
                if (MainForm.Atual != null) MainForm.Atual.RegistrarErroDeTela(e.Exception);
                else MessageBox.Show(e.Exception.ToString(), App.Nome);
            };

            string arquivo = args.FirstOrDefault(a => !a.StartsWith("--") && File.Exists(a));
            string pagina = args.Where(a => a.StartsWith("--pagina=")).Select(a => a.Substring(9)).FirstOrDefault();
            Application.Run(new MainForm(arquivo, pagina));
        }
    }
}
