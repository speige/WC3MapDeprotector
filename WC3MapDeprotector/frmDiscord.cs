using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WC3MapDeprotector
{
    public partial class frmDiscord : Form
    {
        public frmDiscord()
        {
            InitializeComponent();
        }

        private void frmDiscord_Load(object sender, EventArgs e)
        {
            tbUserName.SelectAll();
        }
    }
}
