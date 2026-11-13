using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Genesis.Receitas;

namespace Genesis
{
    /// <summary>Janela "Novo subsistema: Flotador" (o que existe e as variantes de cada item).</summary>
    internal class FlotadorForm : Form, IRetornaResultadoReceita
    {
                /// <summary>Id da receita dona deste form (usado para salvar perfis).</summary>
        public string ReceitaId { get; set; } = "Flotador";
        public OpcoesFlotador Opcoes = new OpcoesFlotador();
        private readonly Func<string, bool> _dbEmUso;
        private readonly List<Action> _ler = new List<Action>();   // copia a tela para as opcoes
        private readonly bool _editando;

        /// <param name="editar">null = novo flotador; com valor = reconfigurar um flotador existente (a janela abre preenchida com essas opcoes).</param>
        public FlotadorForm(string sugestao, List<string> preparadores, Func<string, bool> dbEmUso, OpcoesFlotador editar = null)
        {
            _dbEmUso = dbEmUso;
            _editando = editar != null;
            if (_editando)
            {
                Opcoes = editar;
            }
            else
            {
                Opcoes.Sub = sugestao;
                Opcoes.Descricao = "FLOTADOR " + new string(sugestao.Where(char.IsDigit).ToArray());
                // floculante: o nivel e o do preparador de polimero, se houver um no projeto
                QuimicoFlotador flc = Opcoes.Quimicos.First(q => q.Codigo == "FLC");
                if (preparadores.Count > 0) flc.Nivel = QuimicoFlotador.NivelDoPreparador + preparadores[0];
            }

            Text = _editando ? "Reconfigurar subsistema: " + sugestao : "Novo subsistema: Flotador";
            Size = new Size(1120, 860);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Tema.Janela(this);

            var cab = new FundoDegrade { Dock = DockStyle.Top, Height = 78 };
            var t = Tema.Titulo("Flotador", 16f);
            t.Location = new Point(20, 10);
            var st = Tema.Subtitulo("Marque o que existe · os intertravamentos e modulações já vêm ligados");
            st.Location = new Point(23, 46);
            cab.Controls.Add(t);
            cab.Controls.Add(st);

            var corpo = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true,
                Padding = new Padding(16, 12, 16, 8), BackColor = Tema.Fundo
            };

            // --- geral ---
            var geral = Tabela(4, new[] { 150, 240, 170, 260 });
            var sub = Caixa(Opcoes.Sub, v => Opcoes.Sub = v);
            var desc = Caixa(Opcoes.Descricao, v => Opcoes.Descricao = v);
            Linha(geral, Rot("DB do subsistema"), sub, Rot("Descrição"), desc);
            var vaz = Marca("Medidor de vazão", Opcoes.MedidorVazao, v => Opcoes.MedidorVazao = v);
            Linha(geral, Rot("Vazão"), vaz, new Label(), new Label());
            var niveis = new[] { QuimicoFlotador.NivelAnalogico, QuimicoFlotador.NivelDigital, QuimicoFlotador.NivelNenhum };
            Linha(geral, Rot("Nível equalização"), Lista(niveis, Opcoes.NivelEqualizacao, v => Opcoes.NivelEqualizacao = v),
                Rot("Nível lodo flotado"), Lista(new[] { QuimicoFlotador.NivelAnalogico, QuimicoFlotador.NivelNenhum }, Opcoes.NivelLodo, v => Opcoes.NivelLodo = v));
            corpo.Controls.Add(Cartao("Geral", geral));
            if (_editando)
            {
                var aviso = Tema.Ajuda("Reconfigurar: os itens que continuam mantêm tags e textos que você já preencheu; só muda o que você alterar aqui. "
                    + "Itens novos entram no padrão e os que saem pedem confirmação. Trocar o DB renomeia o subsistema inteiro "
                    + "(o DB antigo que já foi gerado no TIA continua lá até você apagar).", 900);
                aviso.Margin = new Padding(4, 0, 0, 10);
                corpo.Controls.Add(aviso);
            }

            // --- equipamentos ---
            string[] tipos = Gerador.NomesTiposPartida.Where(x => !x.StartsWith("Válvula")).ToArray();
            var eq = Tabela(4, new[] { 250, 280, 125, 290 });
            Action<string, bool, Action<bool>, string, Action<string>, Control, Control> item =
                delegate (string nome, bool marcado, Action<bool> gm, string tipo, Action<string> gt, Control extra1, Control extra2)
                {
                    Linha(eq, Marca(nome, marcado, gm), Lista(tipos, tipo, gt), extra1 ?? new Label(), extra2 ?? new Label());
                };
            item("Agitador tanque equalização", Opcoes.AgitEqualizacao, v => Opcoes.AgitEqualizacao = v, Opcoes.TipoAgitEqu, v => Opcoes.TipoAgitEqu = v, null, null);
            var qtds = new[] { "1", "2", "3" };
            Func<int, Action<int>, ComboBox> qtd = delegate (int v, Action<int> g) { return Lista(qtds, v.ToString(), x => g(int.Parse(x))); };
            item("Bomba alimentação", Opcoes.Alimentacao, v => Opcoes.Alimentacao = v, Opcoes.TipoAlim, v => Opcoes.TipoAlim = v,
                qtd(Opcoes.QtdAlim, v => Opcoes.QtdAlim = v), Marca("Alternância", Opcoes.AltAlim, v => Opcoes.AltAlim = v));
            item("Bombas microbolhas", Opcoes.Microbolhas, v => Opcoes.Microbolhas = v, Opcoes.TipoMic, v => Opcoes.TipoMic = v,
                qtd(Opcoes.QtdMic, v => Opcoes.QtdMic = v), Marca("Alternância", Opcoes.AltMic, v => Opcoes.AltMic = v));
            item("Raspador flotador", Opcoes.Raspador, v => Opcoes.Raspador = v, Opcoes.TipoRas, v => Opcoes.TipoRas = v,
                new Label(),
                Marca("Sensores das pás", Opcoes.SensoresPas, v => Opcoes.SensoresPas = v));
            Linha(eq, Rot("   Raspador: tempo ligado"), Unidade(Opcoes.RaspadorIntermitente, v => Opcoes.RaspadorIntermitente = v, false),
                Nota("unidade do tempo ligado"), new Label());
            Linha(eq, Rot("   Raspador: tempo desligado"), Unidade(Opcoes.RaspadorIntermitenteDes, v => Opcoes.RaspadorIntermitenteDes = v, true),
                Nota("unidade do tempo desligado"), new Label());
            item("Raspador circular", Opcoes.RaspadorCircular, v => Opcoes.RaspadorCircular = v, Opcoes.TipoRasCircular, v => Opcoes.TipoRasCircular = v, null, null);
            var qtdFme = Lista(new[] { "0", "1", "2" }, Opcoes.Floculadores.ToString(), v => Opcoes.Floculadores = int.Parse(v));
            Linha(eq, Rot("Floculador mecânico (agitadores)"), Lista(tipos, Opcoes.TipoFloculador, v => Opcoes.TipoFloculador = v),
                qtdFme, new Label());
            Linha(eq, Rot("   Floculador: tempo ligado"), Unidade(Opcoes.FloculadorIntermitente, v => Opcoes.FloculadorIntermitente = v, false),
                Nota("unidade do tempo ligado"), new Label());
            Linha(eq, Rot("   Floculador: tempo desligado"), Unidade(Opcoes.FloculadorIntermitenteDes, v => Opcoes.FloculadorIntermitenteDes = v, true),
                Nota("unidade do tempo desligado"), new Label());
            item("Bomba lodo flotado", Opcoes.BombaLodo, v => Opcoes.BombaLodo = v, Opcoes.TipoBombaLodo, v => Opcoes.TipoBombaLodo = v,
                qtd(Opcoes.QtdLodo, v => Opcoes.QtdLodo = v), Marca("Alternância", Opcoes.AltLodo, v => Opcoes.AltLodo = v));
            item("Agitador lodo flotado", Opcoes.AgitLodo, v => Opcoes.AgitLodo = v, Opcoes.TipoAgitLodo, v => Opcoes.TipoAgitLodo = v, null, null);
            item("Bomba clarificado", Opcoes.Clarificado, v => Opcoes.Clarificado = v, Opcoes.TipoClarificado, v => Opcoes.TipoClarificado = v,
                qtd(Opcoes.QtdCla, v => Opcoes.QtdCla = v), Marca("Alternância", Opcoes.AltCla, v => Opcoes.AltCla = v));
            Linha(eq, new Label(), new Label(), Cab("bombas"), Nota("microbolhas: soft com bypass = 1 soft para 2 bombas"));
            corpo.Controls.Add(Cartao("Equipamentos", eq));

            // --- quimicos ---
            var qt = Tabela(7, new[] { 150, 64, 200, 104, 80, 180, 140 });
            Linha(qt, Cab("Químico"), Cab("Bombas"), Cab("Tipo das bombas"), Cab("Alternância"), Cab("Agitador"), Cab("Nível do tanque"), Cab("Modulação"));
            foreach (QuimicoFlotador q in Opcoes.Quimicos)
            {
                QuimicoFlotador qq = q;
                var mods = qq.PodePid
                    ? new[] { QuimicoFlotador.ModNenhuma, QuimicoFlotador.ModVazao, QuimicoFlotador.ModPid }
                    : new[] { QuimicoFlotador.ModNenhuma, QuimicoFlotador.ModVazao };
                // o floculante pode usar o nivel do tanque de um preparador de polimero
                string[] niveisQ = qq.Codigo == "FLC"
                    ? preparadores.Select(x => QuimicoFlotador.NivelDoPreparador + x).Concat(niveis).ToArray()
                    : niveis;
                Linha(qt,
                    Marca(Primeira(qq.Nome), qq.Incluir, v => qq.Incluir = v),
                    Lista(new[] { "1", "2", "3" }, qq.Bombas.ToString(), v => qq.Bombas = int.Parse(v)),
                    Lista(tipos, qq.TipoBomba, v => qq.TipoBomba = v),
                    Marca("Sim", qq.Alternancia, v => qq.Alternancia = v),
                    Marca("Sim", qq.Agitador, v => qq.Agitador = v),
                    Lista(niveisQ, qq.Nivel, v => qq.Nivel = v),
                    Lista(mods, qq.Modulacao, v => qq.Modulacao = v));
            }
            var notaQ = Tema.Ajuda("Bombas de químico intertravam por: nível baixo do tanque OU vazão baixa OU bomba(s) de alimentação desligada(s). "
                + "Alternância vale com 2 ou mais bombas (pares na ordem: 1↔2). "
                + "PID de pH cria o medidor de pH (SEN.<DB>.PHI) e o PID na aba PIDs. Tudo pode ser ajustado depois, item por item.", 900);
            corpo.Controls.Add(Cartao("Químicos", qt));
            notaQ.Margin = new Padding(4, 0, 0, 8);
            corpo.Controls.Add(notaQ);

            var botoes = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 62, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(16, 12, 6, 0), BackColor = Tema.FundoEscuro };
            var ok = Tema.Botao(_editando ? "Aplicar alterações" : "Criar flotador", true, delegate { Criar(); });
            var cancela = Tema.Botao("Cancelar", false, delegate { DialogResult = DialogResult.Cancel; });
            botoes.Controls.Add(ok);
            botoes.Controls.Add(cancela);

            Controls.Add(corpo);
            Controls.Add(botoes);
            Controls.Add(cab);
            CancelButton = cancela;
        }

        private static string Primeira(string s)
        {
            s = s.ToLowerInvariant();
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        // --- pecas de layout ---------------------------------------------------------
        private static TableLayoutPanel Tabela(int colunas, int[] larguras)
        {
            var t = new TableLayoutPanel { AutoSize = true, ColumnCount = colunas, BackColor = Color.Transparent, Margin = new Padding(0) };
            foreach (int w in larguras) t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, w));
            return t;
        }

        private static void Linha(TableLayoutPanel t, params Control[] cs)
        {
            int r = t.RowCount;
            t.RowCount = r + 1;
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            for (int i = 0; i < cs.Length && i < t.ColumnCount; i++)
            {
                Control c = Tema.Emoldurar(cs[i]);
                c.Margin = c is CheckBox ? new Padding(0, 10, 10, 3) : c is Label ? new Padding(0, 6, 10, 3) : new Padding(0, 3, 10, 3);
                if (!(c is CheckBox) && !(c is Label)) c.Dock = DockStyle.Fill;
                t.Controls.Add(c, i, r);
            }
        }

        private static Control Cartao(string titulo, Control conteudo)
        {
            var c = new CartaoTabela { AutoSize = true, Margin = new Padding(0, 0, 0, 12) };
            c.ColumnStyles[0].Width = 0;
            var t = Tema.TituloCartao(titulo);
            t.Margin = new Padding(0, 0, 0, 8);
            c.Controls.Add(t, 1, 0);
            c.Controls.Add(conteudo, 1, 1);
            return c;
        }

        private static Label Rot(string s) { return new Label { Text = s, AutoSize = true, Font = Tema.Negrito, ForeColor = Tema.Texto, BackColor = Color.Transparent, Padding = new Padding(0, 5, 0, 0), UseMnemonic = false }; }
        private static Label Cab(string s) { return new Label { Text = Tema.Espacado(s), AutoSize = true, Font = Tema.Rotulo, ForeColor = Tema.TextoSec, BackColor = Color.Transparent }; }
        private static Label Nota(string s) { return new Label { Text = s, AutoSize = true, Font = Tema.Pequena, ForeColor = Tema.TextoSec, BackColor = Color.Transparent, Padding = new Padding(0, 5, 0, 0) }; }

        private TextBox Caixa(string valor, Action<string> grava)
        {
            var tb = Tema.CaixaTexto();
            tb.Text = valor;
            _ler.Add(delegate { grava(tb.Text.Trim()); });
            return tb;
        }

        private CheckBox Marca(string texto, bool valor, Action<bool> grava)
        {
            var cb = Tema.Marcador(texto);
            cb.Checked = valor;
            _ler.Add(delegate { grava(cb.Checked); });
            return cb;
        }

        private ComboBox Lista(string[] itens, string valor, Action<string> grava)
        {
            var cb = Tema.Lista(false);
            cb.Items.AddRange(itens);
            cb.SelectedItem = valor;
            if (cb.SelectedIndex < 0 && cb.Items.Count > 0) cb.SelectedIndex = 0;
            _ler.Add(delegate { grava(Convert.ToString(cb.SelectedItem)); });
            return cb;
        }

        private void SalvarComoPerfil()
        {
            foreach (Action a in _ler) a();   // copia a tela para Opcoes
            using (var dlg = new SalvarPerfilForm("Flotador padrão " + Opcoes.Sub, Opcoes.Descricao))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                var perfil = new PerfilReceita
                {
                    ReceitaId = ReceitaId,
                    Nome = dlg.NomePerfil,
                    Descricao = dlg.DescricaoPerfil,
                    OpcoesXml = PerfisReceita.Serializar(Opcoes, typeof(OpcoesFlotador))
                };
                try
                {
                    PerfisReceita.Salvar(perfil);
                    Dialogo.Info(this, "Perfil \"" + dlg.NomePerfil + "\" salvo. Ele vai aparecer na próxima vez que você criar um Flotador.");
                }
                catch (Exception ex)
                {
                    Dialogo.Erro(this, "Não foi possível salvar o perfil:\n" + ex.Message);
                }
            }
        }
        /// <summary>
        /// Unidade do intermitente: Segundos / Minutos / Horas. Tempo ligado: "Sem intermitente" grava "";
        /// tempo desligado: "Igual ao ligado" grava "".
        /// </summary>
        private ComboBox Unidade(string valor, Action<string> grava, bool desligado)
        {
            string sem = desligado ? "Igual ao ligado" : "Sem intermitente";
            var cb = Tema.Lista(false);
            cb.Items.AddRange(new object[] { sem, "Segundos", "Minutos", "Horas" });
            cb.SelectedItem = string.IsNullOrEmpty(valor) ? sem : valor;
            if (cb.SelectedIndex < 0) cb.SelectedIndex = desligado ? 0 : 2;
            _ler.Add(delegate
            {
                string v = Convert.ToString(cb.SelectedItem);
                grava(v == sem ? "" : v);
            });
            return cb;
        }

                private void Criar()
        {
            foreach (Action a in _ler) a();
            string db = (Opcoes.Sub ?? "").Trim().ToUpperInvariant();
            if (!System.Text.RegularExpressions.Regex.IsMatch(db, "^[A-Z][A-Z0-9_]*$"))
            {
                Dialogo.Aviso(this, "O DB do subsistema precisa começar com letra e ter só letras, números e _.");
                return;
            }
            if (_dbEmUso(db))
            {
                Dialogo.Aviso(this, "Já existe uma máquina ou partida no DB " + db + ". Escolha outro (ex.: FL2).");
                return;
            }
            Opcoes.Sub = db;
            DialogResult = DialogResult.OK;
        }

        // expõe as opções escolhidas para o MainForm via contrato genérico
        ResultadoReceita IRetornaResultadoReceita.Resultado
        {
            get { return new ResultadoReceita { Opcoes = Opcoes }; }
        }
    }   // <-- fecha a classe FlotadorForm (uma chave)
}       // <-- fecha o namespace (uma chave)